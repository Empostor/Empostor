using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Events.Managers;
using Empostor.Api.Localization;
using Empostor.Server.Events.Client;
using Empostor.Server.Net.Hazel;
using Empostor.Server.Net.Manager;
using Empostor.Server.Service.Auth;
using Empostor.Server.Service.Firewall;
using Empostor.Server.Service.Ip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Next.Hazel;
using Next.Hazel.Udp;

namespace Empostor.Server.Net
{
    internal class Matchmaker : IDeltaListenerManager
    {
        private readonly IEventManager _eventManager;
        private readonly ClientManager _clientManager;
        private readonly ObjectPool<MessageReader> _readerPool;
        private readonly ILogger<HazelConnection> _connectionLogger;
        private readonly ILogger<Matchmaker> _logger;
        private readonly PortPoolService _portPool;
        private readonly IFirewallService _firewall;
        private readonly ServerConfig _serverConfig;
        private readonly AuthCacheService _authCache;
        private readonly IOptions<AntiCheatConfig> _antiCheatOptions;
        private readonly IpRateLimitService _rateLimit;
        private readonly ILocalizationService _language;

        private UdpConnectionListener? _mainListener;
        private readonly ConcurrentDictionary<int, UdpConnectionListener> _deltaListeners = new();

        // Shared port for over-quota (rate limited) joins; 0 until first use.
        private int _rejectPort;
        private readonly SemaphoreSlim _rejectPortLock = new(1, 1);

        private IPEndPoint? _mainEndPoint;

        public Matchmaker(
            IEventManager eventManager,
            ClientManager clientManager,
            ObjectPool<MessageReader> readerPool,
            ILogger<HazelConnection> connectionLogger,
            ILogger<Matchmaker> logger,
            PortPoolService portPool,
            IFirewallService firewall,
            IOptions<ServerConfig> serverConfig,
            AuthCacheService authCache,
            IOptions<AntiCheatConfig> antiCheatOptions,
            IpRateLimitService rateLimit,
            ILocalizationService language)
        {
            _eventManager = eventManager;
            _clientManager = clientManager;
            _readerPool = readerPool;
            _connectionLogger = connectionLogger;
            _logger = logger;
            _portPool = portPool;
            _firewall = firewall;
            _serverConfig = serverConfig.Value;
            _authCache = authCache;
            _antiCheatOptions = antiCheatOptions;
            _rateLimit = rateLimit;
            _language = language;

            // Subscribe to port return events from the pool
            _portPool.OnPortReturned += OnPortReturned;
            // NOTE: We deliberately do NOT subscribe to authCache.OnPortExpired.
            // Expiring an auth-cache entry must never return a port or stop its
            // UDP listener, otherwise an active player would be kicked when the
            // auth TTL elapses mid-game. Ports are released only when a player
            // actually disconnects (ClientManager.Remove -> ReturnPort) or when
            // the 5-minute allocation timeout fires (no connection arrived).
        }

        public async ValueTask StartAsync(IPEndPoint ipEndPoint)
        {
            _mainEndPoint = ipEndPoint;

            var mode = ipEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => throw new InvalidOperationException(),
            };

            _mainListener = new UdpConnectionListener(ipEndPoint, _readerPool, mode)
            {
                NewConnection = e => OnNewConnection(e, 0),
            };

            await _mainListener.StartAsync();

            // Open firewall for the main port (delta ports are handled in StartDeltaListenerAsync)
            await _firewall.OpenPortAsync((ushort)ipEndPoint.Port);

            _logger.LogInformation("Matchmaker UDP listener started on {EP}", ipEndPoint);
        }

        /// <summary>
        ///     Records the main endpoint without creating a fixed UDP listener.
        ///     Used when the delta port pool is enabled: no static UDP port is
        ///     bound at startup — ports are only created when players arrive.
        /// </summary>
        public void Initialize(IPEndPoint ipEndPoint)
        {
            _mainEndPoint = ipEndPoint;
            _logger.LogInformation(
                "Matchmaker initialized on {Address} (delta mode, no static UDP port; ports created on demand)",
                ipEndPoint.Address);
        }

        /// <summary>
        ///     Starts a UDP listener on a dynamically allocated delta port.
        /// </summary>
        public async ValueTask<bool> StartDeltaListenerAsync(int port)
        {
            if (_mainEndPoint == null)
            {
                _logger.LogError("Matchmaker cannot start delta listener: main endpoint not initialized");
                return false;
            }

            if (_deltaListeners.ContainsKey(port))
            {
                _logger.LogDebug("Matchmaker delta listener for port {Port} already running", port);
                return true;
            }

            var ep = new IPEndPoint(_mainEndPoint.Address, port);
            var mode = _mainEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => IPMode.IPv4,
            };

            try
            {
                var listener = new UdpConnectionListener(ep, _readerPool, mode)
                {
                    NewConnection = e => OnNewConnection(e, port),
                };

                await listener.StartAsync();
                _deltaListeners[port] = listener;

                // Open firewall for this port
                await _firewall.OpenPortAsync((ushort)port);

                _logger.LogInformation("Matchmaker delta UDP listener started on port {Port}", port);
                return true;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Matchmaker failed to start delta listener on port {Port} (may be in use)", port);
                // Return the port — it's unusable. Report failure to the caller
                // so the token request is rejected and the client is never
                // handed a dead (or soon-reused) port that could match someone
                // else's auth entry.
                _portPool.ReturnPort(port);
                return false;
            }
        }

        public async ValueTask StopDeltaListenerAsync(int port)
        {
            try
            {
                if (_deltaListeners.TryRemove(port, out var listener))
                {
                    try
                    {
                        await listener.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Matchmaker error disposing delta listener on port {Port}", port);
                    }

                    await _firewall.ClosePortAsync((ushort)port);
                    _logger.LogInformation("Matchmaker delta UDP listener stopped on port {Port}", port);
                }
            }
            finally
            {
                // The port's socket is now fully released (whether a listener
                // was disposed here or never existed because bind failed
                // earlier). Only now may the port go back into the allocatable
                // pool, which prevents a new player from racing the old
                // socket's bind.
                _portPool.CompletePortReturn(port);
            }
        }

        /// <summary>
        ///     One shared UDP port for all over-quota joins: reserving it here
        ///     (once) means rate-limited TCP requests never take a pool port and
        ///     never start their own listener. The listener above answers every
        ///     handshake on this port with the localized hint and closes it, so
        ///     the player is told why they cannot join instead of seeing a black
        ///     screen — without letting them into a game.
        /// </summary>
        public async ValueTask<int> GetRateLimitRejectPortAsync()
        {
            if (!_portPool.IsEnabled)
            {
                // Fixed-port mode: the main listener is the reject target.
                return 0;
            }

            var existing = Volatile.Read(ref _rejectPort);
            if (existing > 0)
            {
                return existing;
            }

            await _rejectPortLock.WaitAsync();
            try
            {
                existing = Volatile.Read(ref _rejectPort);
                if (existing > 0)
                {
                    return existing;
                }

                var port = _portPool.AllocatePort("(rate limit reject)");
                if (port <= 0)
                {
                    _logger.LogWarning("Matchmaker could not reserve the rate-limit reject port: pool unavailable");
                    return 0;
                }

                if (!await StartDeltaListenerAsync(port))
                {
                    // StartDeltaListenerAsync already returned the port.
                    return 0;
                }

                // Confirmed so it is never reclaimed by the allocation timeout:
                // this port lives for the whole lifetime of the server.
                _portPool.ConfirmPort(port);
                Volatile.Write(ref _rejectPort, port);

                _logger.LogInformation(
                    "Matchmaker rate-limit reject port reserved on {Port} (one shared port for all over-quota joins)",
                    port);
                return port;
            }
            finally
            {
                _rejectPortLock.Release();
            }
        }

        public async ValueTask StopAsync()
        {
            if (_mainListener != null)
            {
                await _mainListener.DisposeAsync();
            }

            foreach (var (port, listener) in _deltaListeners)
            {
                try
                {
                    await listener.DisposeAsync();
                    _logger.LogDebug("Matchmaker stopped delta listener on port {Port}", port);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Matchmaker error stopping delta listener on port {Port}", port);
                }
            }

            _deltaListeners.Clear();
        }

        private async ValueTask OnNewConnection(NewConnectionEventArgs e, int port)
        {
            HazelConnection? connection = null;

            try
            {
                // Deserialize handshake without matchmakerToken
                Empostor.Api.Net.Messages.C2S.HandshakeC2S.Deserialize(
                    e.HandshakeData,
                    out var clientVersion,
                    out var name,
                    out var language,
                    out var chatMode,
                    out var platformSpecificData);

                connection = new HazelConnection(e.Connection, _connectionLogger, _antiCheatOptions);

                // Shared rate-limit reject port: there is no auth entry and no
                // player behind it, so the only thing to do is tell the client
                // — in its own language — why it is not let in.
                if (port != 0 && port == Volatile.Read(ref _rejectPort))
                {
                    var message = BuildRateLimitMessage(connection.EndPoint?.Address, language);
                    _logger.LogWarning(
                        "Matchmaker rate limit rejected {Ip} on reject port {Port} │ {Message}",
                        connection.EndPoint?.Address?.ToString() ?? "unknown", port, message);

                    await connection.CustomDisconnectAsync(DisconnectReason.Custom, message);
                    return;
                }

                await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));
                await _clientManager.RegisterConnectionAsync(
                    connection, name, clientVersion, language, chatMode, platformSpecificData,
                    deltaPort: port);
            }
            catch (Exception ex)
            {
                // Malformed / unauthenticated UDP handshake. Dispose the
                // underlying connection so an attacker's socket is closed and
                // removed from the listener — never leave it half-initialised.
                _logger.LogWarning(ex, "Rejecting invalid UDP handshake on port {Port}", port);
                connection?.DisposeInnerConnection();
                e.Connection.Dispose();
            }
        }

        // Localized "please try again in N minutes" hint. N is what is actually
        // left for that IP when it can be read off the quota, the full window
        // otherwise (behind a CDN the UDP source address may not be the key the
        // requests were counted with).
        private string BuildRateLimitMessage(IPAddress? ip, Language language)
        {
            var minutes = IpRateLimitService.WindowMinutes;

            if (ip != null && _rateLimit.IsLimited(ip, out var retryAfter))
            {
                minutes = IpRateLimitService.MinutesUntilRetry(retryAfter);
            }

            return _language.Get(new LocalizedMessageKey("empostor", IpRateLimitService.MessageKey), language, minutes);
        }

        private void OnPortReturned(int port)
        {
            _authCache.RemoveByPort(port);
            _ = StopDeltaListenerAsync(port);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Events.Managers;
using Empostor.Server.Events.Client;
using Empostor.Server.Net.Hazel;
using Empostor.Server.Net.Manager;
using Empostor.Server.Service.Auth;
using Empostor.Server.Service.Firewall;
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

        private UdpConnectionListener? _mainListener;
        private readonly ConcurrentDictionary<int, UdpConnectionListener> _deltaListeners = new();

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
            AuthCacheService authCache)
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

            // Subscribe to port return events from the pool
            _portPool.OnPortReturned += OnPortReturned;
            // Auth cache port expiry also needs cleanup
            authCache.OnPortExpired += port => _portPool.ReturnPort(port);
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
            _logger.LogInformation("Matchmaker UDP listener started on {EP}", ipEndPoint);
        }

        /// <summary>
        ///     Starts a UDP listener on a dynamically allocated delta port.
        /// </summary>
        public async ValueTask StartDeltaListenerAsync(int port)
        {
            if (_mainEndPoint == null)
            {
                _logger.LogError("Matchmaker cannot start delta listener: main endpoint not initialized");
                return;
            }

            if (_deltaListeners.ContainsKey(port))
            {
                _logger.LogDebug("Matchmaker delta listener for port {Port} already running", port);
                return;
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
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Matchmaker failed to start delta listener on port {Port} (may be in use)", port);
                // Return the port — it's unusable
                _portPool.ReturnPort(port);
            }
        }

        public async ValueTask StopDeltaListenerAsync(int port)
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
            // Deserialize handshake without matchmakerToken
            Empostor.Api.Net.Messages.C2S.HandshakeC2S.Deserialize(
                e.HandshakeData,
                out var clientVersion,
                out var name,
                out var language,
                out var chatMode,
                out var platformSpecificData);

            var connection = new HazelConnection(e.Connection, _connectionLogger);
            await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));
            await _clientManager.RegisterConnectionAsync(
                connection, name, clientVersion, language, chatMode, platformSpecificData,
                deltaPort: port);
        }

        private void OnPortReturned(int port)
        {
            _authCache.RemoveByPort(port);
            _ = StopDeltaListenerAsync(port);
        }
    }
}

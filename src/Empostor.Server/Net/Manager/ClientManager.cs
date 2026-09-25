using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Events.Managers;
using Empostor.Api.Net;
using Empostor.Api.Net.Manager;
using Empostor.Api.Service;
using Empostor.Server.Events.Client;
using Empostor.Server.Net.Factories;
using Empostor.Server.Service;
using Empostor.Server.Service.Shared;
using Empostor.Server.Service.Auth;
using Empostor.Server.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Next.Hazel;

namespace Empostor.Server.Net.Manager
{
    internal partial class ClientManager
    {
        private readonly ILogger<ClientManager> _logger;
        private readonly IEventManager _eventManager;
        private readonly ConcurrentDictionary<int, ClientBase> _clients;
        private readonly ICompatibilityManager _compatibilityManager;
        private readonly CompatibilityConfig _compatibilityConfig;
        private readonly IClientFactory _clientFactory;
        private readonly AuthCacheService _authCache;
        private readonly PlayerConnectStore _playerConnectStore;
        private readonly PortPoolService _portPool;
        private readonly IpGeolocationService _ipGeo;
        private readonly ClientIdStore _clientIdStore;
        private int _idLast;

        public ClientManager(
            ILogger<ClientManager> logger,
            IEventManager eventManager,
            IClientFactory clientFactory,
            ICompatibilityManager compatibilityManager,
            IOptions<CompatibilityConfig> compatibilityConfig,
            AuthCacheService authCache,
            PlayerConnectStore playerConnectStore,
            PortPoolService portPool,
            IpGeolocationService ipGeo,
            ClientIdStore clientIdStore)
        {
            _logger = logger;
            _eventManager = eventManager;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _compatibilityManager = compatibilityManager;
            _compatibilityConfig = compatibilityConfig.Value;
            _authCache = authCache;
            _playerConnectStore = playerConnectStore;
            _portPool = portPool;
            _ipGeo = ipGeo;
            _clientIdStore = clientIdStore;
            _idLast = checked((int)_clientIdStore.GetLastId());

            if (_compatibilityConfig.AllowFutureGameVersions)
            {
                _logger.LogWarning("AllowFutureGameVersions, which allows future Among Us versions to connect that were unknown at the time this Impostor was built");
            }

            if (_compatibilityConfig.AllowHostAuthority)
            {
                _logger.LogWarning("AllowHostAuthority, which allows game hosts to control more game features, but it uses less well tested code on the client, which causes some bugs");
            }

            if (_compatibilityConfig.AllowVersionMixing)
            {
                _logger.LogWarning("AllowVersionMixing, which allows players to join games created on different game versions that they may not be 100% compatible with");
            }

            if (_compatibilityConfig.AllowFutureGameVersions || _compatibilityConfig.AllowHostAuthority || _compatibilityConfig.AllowVersionMixing)
            {
                _logger.LogWarning("One or more compatibility options were enabled, please mention these when seeking support");
            }
        }

        public IEnumerable<ClientBase> Clients => _clients.Values;

        public int NextId()
        {
            var clientId = Interlocked.Increment(ref _idLast);
            if (clientId < 1) { _idLast = 0; clientId = Interlocked.Increment(ref _idLast); }
            _clientIdStore.SetLastId(clientId);
            return clientId;
        }

        public async ValueTask RegisterConnectionAsync(
            IHazelConnection connection,
            string name,
            GameVersion clientVersion,
            Language language,
            QuickChatModes chatMode,
            PlatformSpecificData? platformSpecificData,
            int deltaPort = 0)
        {
            var id = NextId();

            var versionCompare = _compatibilityManager.CanConnectToServer(clientVersion);
            if (versionCompare == ICompatibilityManager.VersionCompareResult.ServerTooOld
                && _compatibilityConfig.AllowFutureGameVersions && platformSpecificData != null)
            {
                _logger.LogWarning("#{Id} {Name} │ using future version {Version}", id, name, clientVersion);
            }
            else if (versionCompare != ICompatibilityManager.VersionCompareResult.Compatible
                     || platformSpecificData == null)
            {
                _logger.LogInformation("Client connected using unsupported version: {v}", clientVersion);
                using var packet = MessageWriter.Get(MessageType.Reliable);
                var msg = versionCompare switch
                {
                    ICompatibilityManager.VersionCompareResult.ClientTooOld => DisconnectMessages.VersionClientTooOld,
                    ICompatibilityManager.VersionCompareResult.ServerTooOld => DisconnectMessages.VersionServerTooOld,
                    _ => DisconnectMessages.VersionUnsupported,
                };
                await connection.CustomDisconnectAsync(DisconnectReason.Custom, msg);
                return;
            }

            if (clientVersion.HasDisableServerAuthorityFlag)
            {
                if (!_compatibilityConfig.AllowHostAuthority)
                {
                    await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.HostAuthorityUnsupported);
                    return;
                }

                _logger.LogInformation("#{Id} {Name} │ host authority enabled", id, name);
            }

            if (name.Length > 10) { await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameLength); return; }
            if (string.IsNullOrWhiteSpace(name)) { await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameIllegalCharacters); return; }

            string? friendCode = null;
            UserAuthInfo? authInfo = null;
            var clientIp = connection.EndPoint?.Address;
            var location = await _ipGeo.GetLocationAsync(clientIp);
            var locationStr = string.IsNullOrEmpty(location) ? "—" : location;
            var lang = LanguageHelper.GetDisplayName(language);
            var platformStr = platformSpecificData?.Platform.ToString() ?? "Unknown";
            var reactorMods = connection.GetReactorMods();
            var reactorStr = reactorMods?.Mods is { Count: > 0 } mods
                ? $" │ Reactor: {string.Join(", ", System.Linq.Enumerable.Select(mods, m => $"{m.Id} {m.Version}"))}"
                : string.Empty;

            // Match the auth session to this UDP connection. In dynamic delta
            // mode (deltaPort > 0) the connection must match by its unique
            // port (nonce); there is no IP fallback anymore. In fixed-port
            // mode (deltaPort == 0) matching is skipped entirely: no PUID and
            // no FriendCode are bound to the client.
            if (deltaPort > 0)
            {
                authInfo = _authCache.FindByPort(deltaPort);
                if (authInfo == null)
                {
                    // Dynamic delta mode: an active port that has no auth info
                    // bound to it is an unauthenticated UDP connection — reject
                    // it instead of admitting an anonymous client.
                    _logger.LogWarning(
                        "#{Id} {Name} │ port {Port} has no auth info │ {Ip} │ rejecting unauthenticated UDP connection",
                        id, name, deltaPort, NormalizeIp(clientIp));
                    await connection.CustomDisconnectAsync(DisconnectReason.Custom, "Authentication required. Please rejoin through the game server.");
                    return;
                }

                // Port matched — cancel the allocation timeout
                _portPool.ConfirmPort(deltaPort);
                // Mark the auth-cache entry as active so the inactivity
                // timer never clears the port while the player is connected.
                _authCache.ConfirmPort(deltaPort);

                friendCode = authInfo.FriendCode;

                _logger.LogInformation(
                    "#{Id} {Name} │ port {Port} │ {Location} │ {Lang} │ {Platform} │ FC {FriendCode} │ {HashPuid}{Reactor}",
                    id, name, deltaPort, locationStr, lang, platformStr, friendCode ?? "unknown", HashPuid(authInfo.ProductUserId), reactorStr);
            }
            else
            {
                // Fixed-port mode: no auth binding, no PUID/FriendCode.
                _logger.LogInformation(
                    "#{Id} {Name} │ fixed port │ {Location} │ {Lang} │ {Platform}{Reactor}",
                    id, name, locationStr, lang, platformStr, reactorStr);
            }

            var client = _clientFactory.Create(connection, name, clientVersion, language, chatMode, platformSpecificData);
            client.FriendCode = string.IsNullOrEmpty(friendCode) ? null : friendCode;
            client.ProductUserId = authInfo?.ProductUserId;
            client.DeltaPort = deltaPort;

            client.Id = id;
            _logger.LogDebug("#{Id} {Name} │ connected │ FC={FC} │ port {DP}", id, name, client.FriendCode ?? "(none)", deltaPort);
            _clients.TryAdd(id, client);
            await _eventManager.CallAsync(new ClientConnectedEvent(connection, client));
        }

        public void Remove(IClient client)
        {
            _logger.LogDebug("Client [{Id}] removed from client manager", client.Id);
            _clients.TryRemove(client.Id, out _);

            if (client is ClientBase cb && cb.DeltaPort > 0)
            {
                _portPool.ReturnPort(cb.DeltaPort);
            }

            if (!string.IsNullOrEmpty(client.ProductUserId))
            {
                _playerConnectStore.RecordDisconnect(client.ProductUserId);
            }
        }

        public bool Validate(IClient client)
            => client.Id != 0
               && _clients.TryGetValue(client.Id, out var c)
               && ReferenceEquals(client, c);

        private static string NormalizeIp(IPAddress? addr)
        {
            if (addr == null)
            {
                return "(unknown)";
            }

            return addr.IsIPv4MappedToIPv6 ? addr.MapToIPv4().ToString() : addr.ToString();
        }

        private static string HashPuid(string puid)
        {
            if (string.IsNullOrEmpty(puid) || puid.Length < 9)
            {
                return puid ?? "0";
            }

            // Use first 9 characters of the PUID as the short hash
            return puid[..9];
        }
    }
}

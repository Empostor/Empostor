using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net;
using Impostor.Api.Net.Manager;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Factories;
using Impostor.Server.Service.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Next.Hazel;

namespace Impostor.Server.Net.Manager
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
        private int _idLast;

        public ClientManager(
            ILogger<ClientManager> logger,
            IEventManager eventManager,
            IClientFactory clientFactory,
            ICompatibilityManager compatibilityManager,
            IOptions<CompatibilityConfig> compatibilityConfig,
            AuthCacheService authCache)
        {
            _logger = logger;
            _eventManager = eventManager;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _compatibilityManager = compatibilityManager;
            _compatibilityConfig = compatibilityConfig.Value;
            _authCache = authCache;

            if (_compatibilityConfig.AllowFutureGameVersions || _compatibilityConfig.AllowHostAuthority || _compatibilityConfig.AllowVersionMixing)
            {
                _logger.LogWarning("One or more compatibility options were enabled, please mention these when seeking support:");
                if (_compatibilityConfig.AllowFutureGameVersions)
                {
                    _logger.LogWarning("AllowFutureGameVersions");
                }

                if (_compatibilityConfig.AllowHostAuthority)
                {
                    _logger.LogWarning("AllowHostAuthority");
                }

                if (_compatibilityConfig.AllowVersionMixing)
                {
                    _logger.LogWarning("AllowVersionMixing");
                }
            }
        }

        public IEnumerable<ClientBase> Clients => _clients.Values;

        public int NextId()
        {
            var clientId = Interlocked.Increment(ref _idLast);
            if (clientId < 1) { _idLast = 0; clientId = Interlocked.Increment(ref _idLast); }
            return clientId;
        }

        public async ValueTask RegisterConnectionAsync(
            IHazelConnection connection,
            string name,
            GameVersion clientVersion,
            Language language,
            QuickChatModes chatMode,
            PlatformSpecificData? platformSpecificData,
            string? matchmakerToken = null)
        {
            var versionCompare = _compatibilityManager.CanConnectToServer(clientVersion);
            if (versionCompare == ICompatibilityManager.VersionCompareResult.ServerTooOld
                && _compatibilityConfig.AllowFutureGameVersions && platformSpecificData != null)
            {
                _logger.LogWarning("Client connected using future version: {v}", clientVersion);
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

                _logger.LogInformation("Player {Name} connected with host authority.", name);
            }

            if (name.Length > 10) { await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameLength); return; }
            if (string.IsNullOrWhiteSpace(name)) { await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameIllegalCharacters); return; }

            string? friendCode = null;
            var clientIp = connection.EndPoint?.Address;

            var authInfo = _authCache.FindByToken(matchmakerToken);

            if (authInfo != null)
            {
                friendCode = authInfo.FriendCode;
                _logger.LogInformation(
                    "[Auth] {Name}: matched via matchmakerToken → FriendCode={FC} PUID={Puid}",
                    name, friendCode, authInfo.ProductUserId);
            }
            else if (clientIp != null)
            {
                var ipAuth = _authCache.FindByIp(clientIp);
                if (ipAuth != null)
                {
                    friendCode = ipAuth.FriendCode;
                    _logger.LogInformation(
                        "[Auth] {Name}: matched via IP={Ip} → FriendCode={FC}",
                        name, NormalizeIp(clientIp), friendCode);
                }
                else
                {
                    _logger.LogWarning(
                        "[Auth] {Name}: no auth info found (token={Token}, IP={Ip}) — FriendCode will be null",
                        name,
                        string.IsNullOrEmpty(matchmakerToken) ? "(none)" : matchmakerToken[..Math.Min(8, matchmakerToken.Length)] + "...",
                        NormalizeIp(clientIp));
                }
            }

            var client = _clientFactory.Create(connection, name, clientVersion, language, chatMode, platformSpecificData);
            client.FriendCode = string.IsNullOrEmpty(friendCode) ? null : friendCode;

            var id = NextId();
            client.Id = id;
            _logger.LogTrace("Client connected: Id={Id} Name={Name} FriendCode={FC}", id, name, client.FriendCode ?? "(none)");
            _clients.TryAdd(id, client);
            await _eventManager.CallAsync(new ClientConnectedEvent(connection, client));
        }

        public void Remove(IClient client)
        {
            _logger.LogTrace("Client {Id} disconnected.", client.Id);
            _clients.TryRemove(client.Id, out _);
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
    }
}

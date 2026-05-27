using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Events.Managers;
using Empostor.Api.Net;
using Empostor.Api.Net.Manager;
using Empostor.Server.Events.Client;
using Empostor.Server.Net.Factories;
using Empostor.Server.Service;
using Empostor.Server.Service.Auth;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AuthApiConfig _authApiConfig;
        private int _idLast;

        public ClientManager(
            ILogger<ClientManager> logger,
            IEventManager eventManager,
            IClientFactory clientFactory,
            ICompatibilityManager compatibilityManager,
            IOptions<CompatibilityConfig> compatibilityConfig,
            AuthCacheService authCache,
            PlayerConnectStore playerConnectStore,
            IHttpClientFactory httpClientFactory,
            IOptions<AuthApiConfig> authApiConfig)
        {
            _logger = logger;
            _eventManager = eventManager;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _compatibilityManager = compatibilityManager;
            _compatibilityConfig = compatibilityConfig.Value;
            _authCache = authCache;
            _playerConnectStore = playerConnectStore;
            _httpClientFactory = httpClientFactory;
            _authApiConfig = authApiConfig.Value;

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

                if (!string.IsNullOrEmpty(authInfo.VerifyCode) && !authInfo.FriendCodeConfirmed)
                {
                    var nikoResult = await QueryNikoVerifyAsync(authInfo.VerifyCode);
                    if (nikoResult != null)
                    {
                        if (!string.IsNullOrEmpty(nikoResult.Value.Puid)
                            && string.Equals(nikoResult.Value.Puid, authInfo.ProductUserId, StringComparison.OrdinalIgnoreCase))
                        {
                            friendCode = nikoResult.Value.FriendCode;
                            _authCache.UpdateFriendCode(matchmakerToken!, friendCode);
                            _logger.LogInformation(
                                "[Auth] {Name}: Niko PUID matched → FriendCode={FC} PUID={Puid}",
                                name, friendCode, authInfo.ProductUserId);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[Auth] {Name}: Niko PUID mismatch (expected={Expected}, got={Got}) — keeping existing FC",
                                name, authInfo.ProductUserId, nikoResult.Value.Puid);
                        }
                    }
                }

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
            client.ProductUserId = authInfo?.ProductUserId;

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

            if (!string.IsNullOrEmpty(client.ProductUserId))
            {
                _playerConnectStore.RecordDisconnect(client.ProductUserId);
            }
        }

        public bool Validate(IClient client)
            => client.Id != 0
               && _clients.TryGetValue(client.Id, out var c)
               && ReferenceEquals(client, c);

        private async Task<(string FriendCode, string? Puid)?> QueryNikoVerifyAsync(string verifyCode)
        {
            var baseUrl = _authApiConfig.NikoApiBaseUrl.TrimEnd('/');
            var apiUrl = $"{baseUrl}/api/verify";
            var queryUrl = $"{apiUrl}?apikey={Uri.EscapeDataString(_authApiConfig.NikoApiKey)}&verifycode={Uri.EscapeDataString(verifyCode)}";

            try
            {
                using var client = _httpClientFactory.CreateClient("niko");
                var resp = await client.GetAsync(queryUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("VerifyStatus", out var s) ? s.GetString() : null;
                var friendCode = root.TryGetProperty("FriendCode", out var fc) ? fc.GetString() : null;
                var puid = root.TryGetProperty("Puid", out var p) ? p.GetString() : null;

                if (string.IsNullOrEmpty(friendCode)
                    || (status != "HttpPending" && status != "Verified"))
                {
                    _logger.LogDebug("[Auth] Niko GET status={Status} for VerifyCode={Code}", status, verifyCode);
                    return null;
                }

                _logger.LogInformation(
                    "[Auth] Niko GET success: FC={FC} PUID={Puid} Status={Status}",
                    friendCode, puid, status);

                _ = DeleteNikoVerifyAsync(apiUrl, verifyCode);

                return (friendCode, puid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Auth] Niko GET failed for VerifyCode={Code}", verifyCode);
                return null;
            }
        }

        private async Task DeleteNikoVerifyAsync(string apiUrl, string verifyCode)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("niko");
                var body = JsonSerializer.SerializeToUtf8Bytes(new { apikey = _authApiConfig.NikoApiKey, verifycode = verifyCode });
                var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl)
                {
                    Content = new ByteArrayContent(body),
                };
                req.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                await client.SendAsync(req);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

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

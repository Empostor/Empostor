using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Server.Net;
using Empostor.Server.Service.Auth;
using Empostor.Server.Service.Ip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http;

[Route("/api/user")]
[ApiController]
public sealed class TokenController : ControllerBase
{
    private static readonly string CacheFileDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

    private static readonly string CacheFilePath =
        Path.Combine(CacheFileDir, "PuidToFriendCode.txt");

    private static readonly object FileLock = new object();

    private readonly ILogger<TokenController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthCacheService _authCache;
    private readonly AuthApiConfig _authApiConfig;
    private readonly PortPoolService _portPool;
    private readonly IDeltaListenerManager _deltaListenerManager;
    private readonly ServerConfig _serverConfig;

    public TokenController(
        ILogger<TokenController> logger,
        IHttpClientFactory httpClientFactory,
        AuthCacheService authCache,
        IOptions<AuthApiConfig> authApiConfig,
        IOptions<ServerConfig> serverConfig,
        PortPoolService portPool,
        IDeltaListenerManager deltaListenerManager)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _authCache = authCache;
        _authApiConfig = authApiConfig.Value;
        _serverConfig = serverConfig.Value;
        _portPool = portPool;
        _deltaListenerManager = deltaListenerManager;
    }

    [HttpPost]
    public async Task<IActionResult> GetToken(
        [FromBody] TokenRequest request,
        [FromHeader] string authorization)
    {
        try
        {
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            {
                _logger.LogWarning("TokenController missing or invalid Authorization header");
                return Unauthorized(new { error = "Missing or invalid authorization" });
            }

            var eosToken = authorization["Bearer ".Length..];
            var productUserId = ExtractProductUserIdFromJwt(eosToken);
            if (string.IsNullOrEmpty(productUserId))
            {
                _logger.LogWarning("TokenController could not extract PUID from token");
                return Unauthorized(new { error = "Invalid token content" });
            }

            _logger.LogInformation("TokenController request for eosToken={eosToken}", eosToken);

            var clientIp = GetClientIp();
            var friendCode = await GetFriendCodeAsync(eosToken, productUserId);
            var matchmakerToken = GenerateMatchmakerToken(productUserId);

            _authCache.Store(productUserId, matchmakerToken, friendCode, clientIp, eosToken, request.Username, request.ClientVersion);

            // In DeltaPort mode, allocate a dedicated port tying the auth session to the UDP connection.
            // In GameCode mode nothing is allocated (the UDP side matches via a temporary GameCode).
            var deltaPort = _serverConfig.UseGameCodeMode ? 0 : _portPool.AllocatePort(productUserId);

            var authInfo = new UserAuthInfo
            {
                ProductUserId = productUserId,
                MatchmakerToken = matchmakerToken,
                FriendCode = friendCode ?? string.Empty,
                ClientIp = clientIp != null ? NormalizeIpString(clientIp) : null,
                EosToken = eosToken,
                CreatedAt = DateTime.UtcNow,
            };

            // Display name and version as declared by the client; cross-checked on the UDP side.
            authInfo.Name = request.Username ?? string.Empty;
            authInfo.ClientVersion = request.ClientVersion;

            // Display name for logging (inferred from the friend code when the client declares none).
            var playerName = !string.IsNullOrWhiteSpace(authInfo.Name)
                ? authInfo.Name
                : friendCode?.Split('#')[0] ?? productUserId[..Math.Min(8, productUserId.Length)];

            if (deltaPort > 0)
            {
                // Use the dedicated port as a nonce to match the subsequent UDP connection.
                _authCache.StoreByPort(deltaPort, authInfo);

                _logger.LogInformation(
                    "TokenUser {Name} {Puid} {FriendCode} {Ip} {Port} added.",
                    playerName, productUserId, friendCode, clientIp, deltaPort);

                // Make sure the listener is actually started before the port is handed back to the client.
                var listenerStarted = await _deltaListenerManager.StartDeltaListenerAsync(deltaPort);
                if (!listenerStarted)
                {
                    _authCache.RemoveByPort(deltaPort);
                    _logger.LogWarning(
                        "TokenUser {Name} {Puid} rejected: failed to bind delta port {Port}.",
                        playerName, productUserId, deltaPort);

                    return StatusCode(503, new { error = "Server is full, please try again later." });
                }
            }
            else if (_serverConfig.UseGameCodeMode)
            {
                // GameCode mode: no port allocation; matching happens via a temporary GameCode.
                _logger.LogInformation(
                    "TokenUser {Name} {Puid} {FriendCode} {Ip} added (gamecode mode, single port).",
                    playerName, productUserId, friendCode, clientIp);
            }
            else if (_portPool.IsEnabled)
            {
                // DeltaPort mode but the pool is below its low-water mark: reject instead of falling back to IP matching.
                _logger.LogWarning(
                    "TokenUser {Name} {Puid} rejected: dynamic port pool exhausted.",
                    playerName, productUserId);

                return StatusCode(503, new { error = "Server is full, please try again later." });
            }
            else
            {
                // Fixed-port mode: neither dynamic ports nor PUID / FriendCode matching are used.
                _logger.LogInformation(
                    "TokenUser {Name} {Puid} {FriendCode} {Ip} added (fixed port, no matching).",
                    playerName, productUserId, friendCode, clientIp);
            }

            var response = new TokenResponse
            {
                Content = new TokenContent
                {
                    ProductUserId = productUserId,
                    FriendCode = friendCode,
                },
                Hash = matchmakerToken,
                Port = deltaPort,
            };

            return Ok(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenController unexpected error");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private string? ExtractProductUserIdFromJwt(string eosToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(eosToken))
        {
            return null;
        }

        var jwt = handler.ReadJwtToken(eosToken);
        return jwt.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "puid")?.Value;
    }

    private IPAddress? GetClientIp()
    {
        // CDN / reverse proxy aware: X-Real-IP / X-Forwarded-For first.
        return RealIpResolver.Resolve(HttpContext);
    }

    private static string NormalizeIpString(IPAddress? ip)
        => ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4().ToString() : ip?.ToString() ?? "unknown";

    private async Task<string> GetFriendCodeAsync(string eosToken, string productUserId)
    {
        var cached = TryGetFriendCodeFromCache(productUserId);
        if (cached != null)
        {
            _logger.LogInformation(
                "TokenControllerFriendCode found in cache: PUID={Puid} FC={FC}",
                productUserId, cached);
            return cached;
        }

        var mode = _authApiConfig.Mode;
        _logger.LogDebug("TokenControllerAuthApi mode: {Mode}", mode);

        string? friendCode = null;

        if (mode == AuthApiMode.Ume)
        {
            friendCode = await FetchFromUmeAsync(eosToken, productUserId);
        }

        if (string.IsNullOrEmpty(friendCode) && mode == AuthApiMode.Both)
        {
            _logger.LogDebug("TokenController Both mode: trying Ume first, then Innersloth");
            friendCode = await FetchFromUmeAsync(eosToken, productUserId);
            if (string.IsNullOrEmpty(friendCode))
            {
                friendCode = await FetchFromInnerslothAsync(eosToken, productUserId);
            }
        }

        if (string.IsNullOrEmpty(friendCode) && mode == AuthApiMode.Innersloth)
        {
            friendCode = await FetchFromInnerslothAsync(eosToken, productUserId);
        }

        if (string.IsNullOrEmpty(friendCode))
        {
            friendCode = GenerateFallbackFriendCode(productUserId);
            _logger.LogWarning(
                "TokenController FriendCode fetch failed for PUID={Puid}, using fallback: {FC}",
                productUserId, friendCode);
        }
        else
        {
            SaveFriendCodeToCache(productUserId, friendCode);
        }

        return friendCode;
    }

    private static string? TryGetFriendCodeFromCache(string productUserId)
    {
        lock (FileLock)
        {
            if (!System.IO.File.Exists(CacheFilePath))
            {
                return null;
            }

            foreach (var line in System.IO.File.ReadLines(CacheFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0] == productUserId)
                {
                    return parts[1];
                }
            }

            return null;
        }
    }

    private static void SaveFriendCodeToCache(string productUserId, string friendCode)
    {
        lock (FileLock)
        {
            try
            {
                Directory.CreateDirectory(CacheFileDir);

                if (System.IO.File.Exists(CacheFilePath))
                {
                    foreach (var line in System.IO.File.ReadLines(CacheFilePath))
                    {
                        if (line.StartsWith(productUserId + "=", StringComparison.Ordinal))
                        {
                            return;
                        }
                    }
                }

                System.IO.File.AppendAllText(CacheFilePath, $"{productUserId}={friendCode}{Environment.NewLine}");
            }
            catch
            {
                // Best-effort cache — silently ignore write failures (e.g. Docker permission)
            }
        }
    }

    private async Task<string?> FetchFromInnerslothAsync(string eosToken, string productUserId)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("innersloth");
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://backend.innersloth.proxy.amongusclub.cn/api/user/username");
            req.Headers.Add("Authorization", "Bearer " + eosToken);
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.api+json");

            var response = await client.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TokenControllerInnersloth API returned {Status} for PUID={Puid}",
                    response.StatusCode, productUserId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("TokenControllerInnersloth raw response: {Json}", json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var attrs = root.TryGetProperty("data", out var data)
                        && data.TryGetProperty("attributes", out var a) ? a : root;
            var username = attrs.TryGetProperty("username", out var u) ? u.GetString() : null;
            var discriminator = attrs.TryGetProperty("discriminator", out var d) ? d.GetString() : null;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(discriminator))
            {
                var fc = $"{username}#{discriminator}";
                _logger.LogInformation(
                    "TokenControllerFriendCode fetched from Innersloth: PUID={Puid} FC={FC}",
                    productUserId, fc);
                return fc;
            }

            _logger.LogWarning(
                "TokenControllerMissing username/discriminator in Innersloth response for PUID={Puid}",
                productUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenControllerException calling Innersloth API for PUID={Puid}", productUserId);
        }

        return null;
    }

    private async Task<string?> FetchFromUmeAsync(string eosToken, string productUserId)
    {
        if (string.IsNullOrEmpty(_authApiConfig.UmeApiBaseUrl)
            || string.IsNullOrEmpty(_authApiConfig.UmeApiKey))
        {
            _logger.LogWarning("TokenControllerUmeApi config incomplete, skipping Ume");
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("ume");
            var body = JsonSerializer.SerializeToUtf8Bytes(new UmeVerifyRequest
            {
                ApiKey = _authApiConfig.UmeApiKey,
                EosToken = eosToken,
            });

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_authApiConfig.UmeApiBaseUrl.TrimEnd('/')}/api/verify")
            {
                Content = new ByteArrayContent(body),
            };
            req.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TokenControllerUme returned {Status} for PUID={Puid}",
                    resp.StatusCode, productUserId);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("TokenControllerUme raw response: {Json}", json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = root.TryGetProperty("VerifyStatus", out var s) ? s.GetString() : null;
            if (status != "Verified")
            {
                _logger.LogWarning(
                    "TokenControllerUme VerifyStatus={Status} for PUID={Puid}",
                    status, productUserId);
                return null;
            }

            var friendCode = root.TryGetProperty("FriendCode", out var fc) ? fc.GetString() : null;
            if (string.IsNullOrEmpty(friendCode))
            {
                _logger.LogWarning(
                    "TokenControllerUme response missing FriendCode for PUID={Puid}",
                    productUserId);
                return null;
            }

            var returnedPuid = root.TryGetProperty("ProductUserId", out var rp) ? rp.GetString() : null;
            if (string.IsNullOrEmpty(returnedPuid)
                || !string.Equals(returnedPuid, productUserId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "TokenControllerUme PUID mismatch: expected={Expected} got={Got}",
                    productUserId, returnedPuid);
                return null;
            }

            _logger.LogInformation(
                "TokenControllerFriendCode from Ume: PUID={Puid} FC={FC}",
                productUserId, friendCode);
            return friendCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenControllerException calling Ume for PUID={Puid}", productUserId);
        }

        return null;
    }

    private static string GenerateMatchmakerToken(string productUserId)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var input = $"{productUserId}:{DateTime.UtcNow.Ticks}:{Convert.ToBase64String(salt)}";
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static string GenerateFallbackFriendCode(string productUserId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(productUserId));
        var disc = BitConverter.ToUInt16(hash, 0) % 10000;
        return $"failauth#{disc:D4}";
    }

    #region Request Datas

    public sealed class TokenRequest
    {
        [JsonPropertyName("Puid")]
        public required string Puid { get; init; }

        [JsonPropertyName("Username")]
        public string? Username { get; init; }

        [JsonPropertyName("ClientVersion")]
        public int ClientVersion { get; init; }
    }

    public sealed class TokenResponse
    {
        [JsonPropertyName("Content")]
        public required TokenContent Content { get; init; }

        [JsonPropertyName("Hash")]
        public required string Hash { get; init; }

        [JsonPropertyName("Port")]
        public int Port { get; init; }
    }

    public sealed class TokenContent
    {
        [JsonPropertyName("Puid")]
        public required string ProductUserId { get; init; }

        [JsonPropertyName("FriendCode")]
        public string? FriendCode { get; init; }

        [JsonPropertyName("ExpiresAt")]
        public DateTime ExpiresAt { get; init; } = new DateTime(2012, 12, 21);
    }

    private sealed class UmeVerifyRequest
    {
        [JsonPropertyName("ApiKey")]
        public required string ApiKey { get; init; }

        [JsonPropertyName("EosToken")]
        public required string EosToken { get; init; }
    }
    #endregion
}

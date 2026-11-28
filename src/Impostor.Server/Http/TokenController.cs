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
using Impostor.Api.Config;
using Impostor.Server.Net;
using Impostor.Server.Service.Auth;
using Impostor.Server.Service.Ip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Http;

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
    private readonly PortPoolService _portPool;
    private readonly IDeltaListenerManager _deltaListenerManager;
    private readonly ServerConfig _serverConfig;

    public TokenController(
        ILogger<TokenController> logger,
        IHttpClientFactory httpClientFactory,
        AuthCacheService authCache,
        IOptions<ServerConfig> serverConfig,
        PortPoolService portPool,
        IDeltaListenerManager deltaListenerManager)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _authCache = authCache;
        _serverConfig = serverConfig.Value;
        _portPool = portPool;
        _deltaListenerManager = deltaListenerManager;
    }

    [HttpPost]
    public async Task<IActionResult> GetToken(
        [FromBody] TokenRequest request,
        [FromHeader] string? authorization)
    {
        if (_serverConfig.IsVerificationDisabled)
        {
            var productUserId = request.Puid ?? request.ProductUserId;
            if (productUserId == null)
            {
                return BadRequest(new { error = "Missing Puid" });
            }

            // Keep the legacy response shape without consulting EOS, the auth cache, or either verification flow.
            var legacyToken = new
            {
                Content = new TokenContent
                {
                    ProductUserId = productUserId,
                    ClientVersion = request.ClientVersion,
                },
                Hash = "impostor_was_here",
            };

            return Ok(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(legacyToken)));
        }

        var allocatedDeltaPort = 0;
        string? matchmakerToken = null;
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

            _logger.LogDebug("TokenController request for PUID={Puid}", productUserId);

            var clientIp = GetClientIp();
            var friendCode = await GetFriendCodeAsync(eosToken, productUserId);
            matchmakerToken = GenerateMatchmakerToken(productUserId);

            // Only DeltaPort mode allocates a dedicated port to match the HTTP session to the UDP connection.
            var deltaPort = _serverConfig.UseDeltaPortMode ? _portPool.AllocatePort(productUserId) : 0;
            allocatedDeltaPort = deltaPort;

            var authInfo = new UserAuthInfo
            {
                ProductUserId = productUserId,
                MatchmakerToken = matchmakerToken,
                FriendCode = friendCode,
                ClientIp = clientIp != null ? NormalizeIpString(clientIp) : null,
                EosToken = eosToken,
                CreatedAt = DateTime.UtcNow,
                Port = deltaPort,
            };

            // Display name and version as declared by the client; cross-checked on the UDP side.
            authInfo.Name = request.Username ?? string.Empty;
            authInfo.ClientVersion = request.ClientVersion;

            // Display name for logging (inferred from the friend code when the client declares none).
            var playerName = !string.IsNullOrWhiteSpace(authInfo.Name)
                ? authInfo.Name
                : friendCode.Split('#')[0];

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
                    _portPool.ReturnPort(deltaPort);
                    allocatedDeltaPort = 0;
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

            _authCache.Store(productUserId, matchmakerToken, friendCode, clientIp, eosToken, request.Username, request.ClientVersion, deltaPort);

            var response = new Token
            {
                Content = new TokenContent
                {
                    ProductUserId = productUserId,
                    FriendCode = friendCode,
                    ClientVersion = request.ClientVersion,
                },
                Hash = matchmakerToken,
                Port = deltaPort,
            };

            return Ok(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(response)));
        }
        catch (Exception ex)
        {
            if (allocatedDeltaPort > 0)
            {
                _authCache.RemoveByPort(allocatedDeltaPort);
                _portPool.ReturnPort(allocatedDeltaPort);
            }
            if (!string.IsNullOrEmpty(matchmakerToken)) _authCache.RemoveByToken(matchmakerToken);
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

        var friendCode = await FetchFromInnerslothAsync(eosToken, productUserId);
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
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://backend.innersloth.proxy.amongusclub.cn/api/user/username");
            req.Headers.Add("Authorization", "Bearer " + eosToken);
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.api+json");

            using var response = await client.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TokenControllerInnersloth API returned {Status} for PUID={Puid}",
                    response.StatusCode, productUserId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
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
        public string? Puid { get; init; }

        [JsonPropertyName("ProductUserId")]
        public string? ProductUserId { get; init; }

        [JsonPropertyName("Username")]
        public string? Username { get; init; }

        [JsonPropertyName("ClientVersion")]
        public int ClientVersion { get; init; }
    }

    public sealed class Token
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FriendCode { get; init; }

        [JsonPropertyName("ClientVersion")]
        public int ClientVersion { get; init; }

        [JsonPropertyName("ExpiresAt")]
        public DateTime ExpiresAt { get; init; } = new DateTime(2012, 12, 21);
    }

    #endregion
}

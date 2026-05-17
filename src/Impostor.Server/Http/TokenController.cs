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
using Impostor.Server.Service.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

    public TokenController(
        ILogger<TokenController> logger,
        IHttpClientFactory httpClientFactory,
        AuthCacheService authCache)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _authCache = authCache;
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
                _logger.LogWarning("[TokenController] Missing or invalid Authorization header");
                return Unauthorized(new { error = "Missing or invalid authorization" });
            }

            var eosToken = authorization["Bearer ".Length..];
            var productUserId = ExtractProductUserIdFromJwt(eosToken);
            if (string.IsNullOrEmpty(productUserId))
            {
                _logger.LogWarning("[TokenController] Could not extract PUID from token");
                return Unauthorized(new { error = "Invalid token content" });
            }

            var friendCode = await GetFriendCodeAsync(eosToken, productUserId);
            var matchmakerToken = GenerateMatchmakerToken(productUserId);
            var clientIp = GetClientIp();

            _authCache.Store(productUserId, matchmakerToken, friendCode, clientIp);

            _logger.LogInformation(
                "[TokenController] Authenticated: PUID={Puid} FriendCode={FC} IP={Ip}",
                productUserId, friendCode, clientIp);

            var response = new TokenResponse
            {
                Content = new TokenContent
                {
                    ProductUserId = productUserId,
                    FriendCode = friendCode,
                },
                Hash = matchmakerToken,
            };

            return Ok(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenController] Unexpected error");
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
        var xRealIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp) && IPAddress.TryParse(xRealIp, out var realIp))
        {
            return Normalize(realIp);
        }

        var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var first = xForwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out var fwdIp))
            {
                return Normalize(fwdIp);
            }
        }

        return Normalize(HttpContext.Connection.RemoteIpAddress);
    }

    private static IPAddress? Normalize(IPAddress? ip)
        => ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip;

    private async Task<string> GetFriendCodeAsync(string eosToken, string productUserId)
    {
        var cached = TryGetFriendCodeFromCache(productUserId);
        if (cached != null)
        {
            _logger.LogInformation(
                "[TokenController] FriendCode found in cache: PUID={Puid} FC={FC}",
                productUserId, cached);
            return cached;
        }

        var friendCode = await FetchFromInnerslothAsync(eosToken, productUserId);
        if (string.IsNullOrEmpty(friendCode))
        {
            friendCode = GenerateFallbackFriendCode(productUserId);
            _logger.LogWarning(
                "[TokenController] FriendCode fetch failed for PUID={Puid}, using fallback: {FC}",
                productUserId, friendCode);
        }

        SaveFriendCodeToCache(productUserId, friendCode);

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
    }

    private async Task<string?> FetchFromInnerslothAsync(string eosToken, string productUserId)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("innersloth");
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://backend.innersloth.com/api/user/username");
            req.Headers.Add("Authorization", "Bearer " + eosToken);
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.api+json");

            var response = await client.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[TokenController] Innersloth API returned {Status} for PUID={Puid}",
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
                    "[TokenController] FriendCode fetched: PUID={Puid} FC={FC}",
                    productUserId, fc);
                return fc;
            }

            _logger.LogWarning(
                "[TokenController] Missing username/discriminator in Innersloth response for PUID={Puid}",
                productUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenController] Exception calling Innersloth API for PUID={Puid}", productUserId);
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
        return $"Player#{disc:D4}";
    }

    public sealed class TokenRequest
    {
        [JsonPropertyName("Puid")]
        public required string Puid { get; init; }
    }

    public sealed class TokenResponse
    {
        [JsonPropertyName("Content")]
        public required TokenContent Content { get; init; }

        [JsonPropertyName("Hash")]
        public required string Hash { get; init; }
    }

    public sealed class TokenContent
    {
        [JsonPropertyName("Puid")]
        public required string ProductUserId { get; init; }

        [JsonPropertyName("FriendCode")]
        public string? FriendCode { get; init; }

        [JsonPropertyName("ExpiresAt")]
        public DateTime ExpiresAt { get; init; } = new DateTime(2099, 12, 31);
    }
}

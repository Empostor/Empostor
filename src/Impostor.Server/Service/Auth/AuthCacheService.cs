using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Auth;

public sealed class AuthCacheService : IDisposable
{
    private readonly ILogger<AuthCacheService> _logger;

    private readonly ConcurrentDictionary<string, UserAuthInfo> _byToken = new();
    private readonly ConcurrentDictionary<string, string> _byIp = new();

    private readonly Timer _cleanupTimer;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public AuthCacheService(ILogger<AuthCacheService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(_ => Cleanup(), null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void Store(string productUserId, string matchmakerToken, string? friendCode, IPAddress? clientIp)
    {
        if (string.IsNullOrEmpty(productUserId) || string.IsNullOrEmpty(matchmakerToken))
        {
            throw new ArgumentException("PUID and matchmakerToken cannot be null or empty");
        }

        var info = new UserAuthInfo
        {
            ProductUserId = productUserId,
            MatchmakerToken = matchmakerToken,
            FriendCode = friendCode ?? string.Empty,
            ClientIp = clientIp != null ? NormalizeIp(clientIp) : null,
            CreatedAt = DateTime.UtcNow,
        };

        _byToken[matchmakerToken] = info;

        if (clientIp != null)
        {
            var key = NormalizeIp(clientIp);
            _byIp[key] = matchmakerToken;
            if (clientIp.IsIPv4MappedToIPv6)
            {
                _byIp[clientIp.MapToIPv4().ToString()] = matchmakerToken;
            }
        }

        _logger.LogDebug("[Auth] Stored PUID={Puid} FC={FC}", productUserId, friendCode ?? "(none)");
    }

    public UserAuthInfo? FindByToken(string? matchmakerToken)
    {
        if (string.IsNullOrEmpty(matchmakerToken))
        {
            return null;
        }

        return _byToken.TryGetValue(matchmakerToken, out var info) && !Expired(info) ? info : null;
    }

    public UserAuthInfo? FindByIp(IPAddress? clientIp)
    {
        if (clientIp == null)
        {
            return null;
        }

        var key = NormalizeIp(clientIp);
        return _byIp.TryGetValue(key, out var token) ? FindByToken(token) : null;
    }

    public (int TokenCount, int IpMappingCount) GetStats()
        => (_byToken.Count, _byIp.Count);

    private void Cleanup()
    {
        var expired = _byToken.Where(kv => Expired(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var token in expired)
        {
            if (_byToken.TryRemove(token, out var info) && info.ClientIp != null)
            {
                _byIp.TryRemove(info.ClientIp, out _);
            }
        }

        if (expired.Count > 0)
        {
            _logger.LogDebug("[Auth] Cleaned {Count} expired entries", expired.Count);
        }
    }

    private static bool Expired(UserAuthInfo info)
        => DateTime.UtcNow - info.CreatedAt > Ttl;

    private static string NormalizeIp(IPAddress ip)
        => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

    public void Dispose() => _cleanupTimer.Dispose();
}

public sealed class UserAuthInfo
{
    public string ProductUserId { get; set; } = string.Empty;

    public string MatchmakerToken { get; set; } = string.Empty;

    public string FriendCode { get; set; } = string.Empty;

    public string? ClientIp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

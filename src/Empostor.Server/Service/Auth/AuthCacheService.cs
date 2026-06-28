using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Auth;

public sealed class AuthCacheService : IDisposable
{
    private readonly ILogger<AuthCacheService> _logger;

    private readonly ConcurrentDictionary<string, UserAuthInfo> _byToken = new();
    private readonly ConcurrentDictionary<string, string> _byIp = new();
    private readonly ConcurrentDictionary<int, UserAuthInfo> _byPort = new();
    private readonly ConcurrentDictionary<string, UserAuthInfo> _byIpDirect = new();
    private readonly ConcurrentDictionary<string, int> _ipToPort = new();

    /// <summary>
    ///     Invoked when a port lease should be returned to the pool (e.g., on expiry during cleanup).
    /// </summary>
    public event Action<int>? OnPortExpired;

    private readonly Timer _cleanupTimer;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public AuthCacheService(ILogger<AuthCacheService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(_ => Cleanup(), null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void Store(string productUserId, string matchmakerToken, string? friendCode, IPAddress? clientIp,
        string? verifyCode = null, bool friendCodeConfirmed = false)
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
            VerifyCode = verifyCode,
            FriendCodeConfirmed = friendCodeConfirmed,
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

    /// <summary>
    ///     Stores auth info keyed by delta port. Also maps the IP for fallback lookups.
    /// </summary>
    public void StoreByPort(int port, UserAuthInfo info)
    {
        _byPort[port] = info;

        if (info.ClientIp != null)
        {
            _byIpDirect[info.ClientIp] = info;
            _ipToPort[info.ClientIp] = port;
        }
    }

    /// <summary>
    ///     Looks up the delta port assigned to a client IP.
    ///     Returns 0 if no port is assigned (IP fallback or expired).
    /// </summary>
    public int FindPortByIp(IPAddress? clientIp)
    {
        if (clientIp == null)
        {
            return 0;
        }

        var key = NormalizeIp(clientIp);
        return _ipToPort.TryGetValue(key, out var port) ? port : 0;
    }

    /// <summary>
    ///     Stores auth info keyed by IP only (used when port pool is exhausted or feature disabled).
    /// </summary>
    public void StoreByIp(string ip, UserAuthInfo info)
    {
        _byIpDirect[ip] = info;
    }

    /// <summary>
    ///     Looks up auth info by delta port number.
    /// </summary>
    public UserAuthInfo? FindByPort(int port)
    {
        return _byPort.TryGetValue(port, out var info) && !Expired(info) ? info : null;
    }

    /// <summary>
    ///     Removes a port entry from all lookup dictionaries.
    /// </summary>
    public void RemoveByPort(int port)
    {
        if (_byPort.TryRemove(port, out var info) && info.ClientIp != null)
        {
            _byIpDirect.TryRemove(info.ClientIp, out _);
            _ipToPort.TryRemove(info.ClientIp, out _);
        }
    }

    public UserAuthInfo? FindByIp(IPAddress? clientIp)
    {
        if (clientIp == null)
        {
            return null;
        }

        var key = NormalizeIp(clientIp);

        // Try direct IP storage first (new path: port-based or IP-only)
        if (_byIpDirect.TryGetValue(key, out var info) && !Expired(info))
        {
            return info;
        }

        // Fall back to legacy token-based lookup
        return _byIp.TryGetValue(key, out var token) ? FindByToken(token) : null;
    }

    public (int TokenCount, int IpMappingCount) GetStats()
        => (_byToken.Count, _byIp.Count);

    public bool UpdateFriendCode(string matchmakerToken, string friendCode)
    {
        if (!_byToken.TryGetValue(matchmakerToken, out var info) || Expired(info))
        {
            return false;
        }

        info.FriendCode = friendCode;
        info.FriendCodeConfirmed = true;
        return true;
    }

    private void Cleanup()
    {
        // Clean token-based entries
        var expired = _byToken.Where(kv => Expired(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var token in expired)
        {
            if (_byToken.TryRemove(token, out var info) && info.ClientIp != null)
            {
                _byIp.TryRemove(info.ClientIp, out _);
            }
        }

        // Clean port-based entries
        var expiredPorts = _byPort.Where(kv => Expired(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var port in expiredPorts)
        {
            if (_byPort.TryRemove(port, out var info))
            {
                if (info.ClientIp != null)
                {
                    _byIpDirect.TryRemove(info.ClientIp, out _);
                    _ipToPort.TryRemove(info.ClientIp, out _);
                }

                OnPortExpired?.Invoke(port);
            }
        }

        // Clean direct IP entries
        var expiredIps = _byIpDirect.Where(kv => Expired(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var ip in expiredIps)
        {
            _byIpDirect.TryRemove(ip, out _);
            _ipToPort.TryRemove(ip, out _);
        }

        var totalExpired = expired.Count + expiredPorts.Count + expiredIps.Count;
        if (totalExpired > 0)
        {
            _logger.LogDebug("[Auth] Cleaned {Count} expired entries (token={T}, port={P}, ip={I})",
                totalExpired, expired.Count, expiredPorts.Count, expiredIps.Count);
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

    public string? VerifyCode { get; set; }

    public bool FriendCodeConfirmed { get; set; }
}

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
    private readonly ConcurrentDictionary<int, UserAuthInfo> _byPort = new();

    /// <summary>
    ///     Ports that have an active connection. While a player is connected,
    ///     the port lease must not be cleared by the inactivity timer.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> _confirmedPorts = new();

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

        _logger.LogDebug("AuthCache stored PUID={Puid} FC={FC}", productUserId, friendCode ?? "(none)");
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
    ///     Stores auth info keyed by delta port.
    /// </summary>
    public void StoreByPort(int port, UserAuthInfo info)
    {
        _byPort[port] = info;
    }

    public UserAuthInfo? FindByPort(int port)
    {
        return _byPort.TryGetValue(port, out var info) && !Expired(info) ? info : null;
    }

    /// <summary>
    ///     Marks a delta port as having an active connection.
    ///     The inactivity timer will skip confirmed ports so that connected
    ///     players are never kicked because of the auth-cache TTL.
    /// </summary>
    public void ConfirmPort(int port)
    {
        if (port <= 0)
        {
            return;
        }

        _confirmedPorts[port] = 0;

        // Refresh the expiry so the entry is also valid for other lookups.
        if (_byPort.TryGetValue(port, out var info))
        {
            info.CreatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveByPort(int port)
    {
        _confirmedPorts.TryRemove(port, out _);
        _byPort.TryRemove(port, out _);
    }

    public (int TokenCount, int PortCount) GetStats()
        => (_byToken.Count, _byPort.Count);

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
            _byToken.TryRemove(token, out _);
        }

        // Clean port-based entries
        // Confirmed (actively connected) ports are skipped: the lease is only
        // released when the player disconnects.
        var expiredPorts = _byPort
            .Where(kv => !_confirmedPorts.ContainsKey(kv.Key) && Expired(kv.Value))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var port in expiredPorts)
        {
            if (_byPort.TryRemove(port, out var info))
            {
                _logger.LogInformation(
                    "TokenUser {Name} {FriendCode} removed for inactivity timer. Port: {Port}",
                    info.Name, info.FriendCode, port);

                OnPortExpired?.Invoke(port);
            }
        }

        var totalExpired = expired.Count + expiredPorts.Count;
        if (totalExpired > 0)
        {
            _logger.LogDebug("AuthCache cleaned {Count} expired entries",
                totalExpired);
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

    public string Name { get; set; } = string.Empty;

    public string? ClientIp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? VerifyCode { get; set; }

    public bool FriendCodeConfirmed { get; set; }
}

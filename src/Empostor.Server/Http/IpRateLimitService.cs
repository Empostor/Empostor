using System;
using System.Collections.Concurrent;
using System.Net;
using Empostor.Api.Config;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http;

// Per-IP request counter used by the anticheat "Ip request rate limit".
// Every real IP gets a fixed window (default 5 minutes); when the IP sends
// more TCP (HTTP) requests than configured (default 30) it is blocked until
// the window slides away.
public sealed class IpRateLimitService
{
    // Language key of the localized hint, see Languages/*.json.
    public const string MessageKey = "ratelimit.too_frequent";

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(60);

    private readonly IOptions<AntiCheatConfig> _options;
    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public IpRateLimitService(IOptions<AntiCheatConfig> options)
    {
        _options = options;
    }

    // Counts the request against the IP. Returns false when the IP exhausted
    // its quota; retryAfter then tells how long the caller still has to wait.
    public bool TryRegister(IPAddress? ip, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        var config = _options.Value;
        var window = GetWindow(config);
        if (ip == null || window == null || config.IpRateLimitRequests <= 0)
        {
            return true;
        }

        var value = window.Value;
        Cleanup(value);

        var now = DateTime.UtcNow;
        var entry = _windows.GetOrAdd(ip.ToString(), _ => new Window(now));

        lock (entry)
        {
            if (now - entry.StartUtc >= value)
            {
                entry.StartUtc = now;
                entry.Count = 0;
            }

            entry.Count++;
            if (entry.Count <= config.IpRateLimitRequests)
            {
                return true;
            }

            // Blocked requests do not restart the window, so the lockout ends
            // at most one full window after the first request of the burst.
            retryAfter = entry.StartUtc + value - now;
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }

            return false;
        }
    }

    // Reads the quota without counting a request — used to answer the client
    // in-game (and to skip expensive work) when the IP is already over quota.
    public bool IsLimited(IPAddress? ip, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        var config = _options.Value;
        var window = GetWindow(config);
        if (ip == null || window == null || config.IpRateLimitRequests <= 0)
        {
            return false;
        }

        if (!_windows.TryGetValue(ip.ToString(), out var entry))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        lock (entry)
        {
            if (now - entry.StartUtc >= window.Value || entry.Count <= config.IpRateLimitRequests)
            {
                return false;
            }

            retryAfter = entry.StartUtc + window.Value - now;
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }

            return true;
        }
    }

    private static TimeSpan? GetWindow(AntiCheatConfig config)
    {
        if (!config.EnableIpRateLimit)
        {
            return null;
        }

        return TimeSpan.FromMinutes(Math.Max(1, config.IpRateLimitWindowMinutes));
    }

    // Whole minutes to wait, always at least one so the hint never says
    // "please try again in 0 minutes".
    public static int MinutesUntilRetry(TimeSpan retryAfter)
    {
        var minutes = (int)Math.Ceiling(retryAfter.TotalMinutes);
        return minutes < 1 ? 1 : minutes;
    }

    private void Cleanup(TimeSpan window)
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanupUtc < CleanupInterval)
        {
            return;
        }

        _lastCleanupUtc = now;
        foreach (var pair in _windows)
        {
            if (now - pair.Value.StartUtc > window + CleanupInterval)
            {
                _windows.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class Window
    {
        public Window(DateTime startUtc)
        {
            StartUtc = startUtc;
        }

        public DateTime StartUtc { get; set; }

        public int Count { get; set; }
    }
}

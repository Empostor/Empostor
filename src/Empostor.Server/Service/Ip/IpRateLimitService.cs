using System;
using System.Collections.Concurrent;
using System.Net;
using Empostor.Api.Config;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Ip;
public sealed class IpRateLimitService
{
    public const string MessageKey = "ratelimit.too_frequent";

    public const int MaxRequestsPerWindow = 30;

    public const int WindowMinutes = 1;

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
        if (ip == null || window == null)
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
            if (entry.Count <= MaxRequestsPerWindow)
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
        if (ip == null || window == null)
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
            if (now - entry.StartUtc >= window.Value || entry.Count <= MaxRequestsPerWindow)
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
        if (!config.EnableRateLimits)
        {
            return null;
        }

        return TimeSpan.FromMinutes(WindowMinutes);
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

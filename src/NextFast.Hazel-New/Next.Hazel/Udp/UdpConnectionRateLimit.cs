using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Serilog;

namespace Next.Hazel.Udp;

public class UdpConnectionRateLimit : IDisposable
{
    // Allow burst to 5 connections.
    // Decrease by 1 every second.
    public int MaxConnections { get;  private set; } = 5;

    public int FalloffMs { get; private set; } = 1000;
    private static readonly ILogger Logger = Log.ForContext<UdpConnectionRateLimit>();

    private readonly ConcurrentDictionary<IPAddress, int> _connectionCount;
    private IReadOnlyDictionary<IPAddress, int> _ConnectionCount => _connectionCount.AsReadOnly();
    private readonly Timer _timer;
    private bool _isDisposed;

    public void Reset(int maxConnections, int falloffMs)
    {
        MaxConnections = maxConnections;
        FalloffMs = falloffMs;
        _timer.Change(FalloffMs, Timeout.Infinite);
    }

    public UdpConnectionRateLimit()
    {
        _connectionCount = new ConcurrentDictionary<IPAddress, int>();
        _timer = new Timer(UpdateRateLimit, null, FalloffMs, Timeout.Infinite);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _timer.Dispose();
    }

    private void UpdateRateLimit(object state)
    {
        try
        {
            foreach (var pair in _connectionCount)
            {
                var count = pair.Value - 1;
                if (count > 0)
                    _connectionCount.TryUpdate(pair.Key, count, pair.Value);
                else
                    _connectionCount.TryRemove(pair);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Exception caught in UpdateRateLimit.");
        }
        finally
        {
            if (!_isDisposed) _timer.Change(FalloffMs, Timeout.Infinite);
        }
    }

    public bool IsAllowed(IPAddress key)
    {
        if (_connectionCount.TryGetValue(key, out var value) && value >= MaxConnections) 
            return false;

        _connectionCount.AddOrUpdate(key, _ => 1, (_, i) => i + 1);
        return true;
    }
}
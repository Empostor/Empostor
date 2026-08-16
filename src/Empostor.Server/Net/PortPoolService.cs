using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Net;

/// <summary>
///     Thread-safe pool of UDP ports from the configured delta range.
///     Allocates a unique port per authenticated player to act as a nonce
///     for matching the TCP auth session to the subsequent UDP connection.
/// </summary>
public sealed class PortPoolService : IDisposable
{
    private readonly ILogger<PortPoolService> _logger;
    private readonly ConcurrentBag<int> _availablePorts = new();
    private readonly ConcurrentDictionary<int, PortLease> _activeLeases = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _timeouts = new();
    private readonly ConcurrentDictionary<int, byte> _draining = new();
    private readonly int _deltaPortStart;
    private readonly int _deltaPortEnd;
    private readonly int _listenPort;
    private readonly int _deltaLowWaterMark;

    public bool IsEnabled => _deltaPortStart > 0
                             && _deltaPortEnd >= _deltaPortStart;

    /// <summary>
    ///     Invoked when a port is returned to the pool (via disconnect or timeout expiry).
    ///     Subscribers should stop the delta listener and close firewall rules,
    ///     then call <see cref="CompletePortReturn" /> once the socket is fully disposed.
    /// </summary>
    public event Action<int>? OnPortReturned;

    public PortPoolService(
        ILogger<PortPoolService> logger,
        IOptions<ServerConfig> serverConfig)
    {
        _logger = logger;

        var cfg = serverConfig.Value;
        _deltaPortStart = cfg.DeltaPortStart;
        _deltaPortEnd = cfg.DeltaPortEnd;
        _listenPort = cfg.ListenPort;
        _deltaLowWaterMark = cfg.DeltaPortLowWaterMark;

        if (IsEnabled)
        {
            var skipped = 0;
            for (var port = _deltaPortStart; port <= _deltaPortEnd; port++)
            {
                if (port == _listenPort)
                {
                    skipped++;
                    continue;
                }

                _availablePorts.Add(port);
            }

            if (skipped > 0)
            {
                _logger.LogWarning(
                    "PortPool skipped {Count} port(s) overlapping with the main listen port {ListenPort}",
                    skipped, _listenPort);
            }

            _logger.LogInformation(
                "PortPool initialized with {Count} ports ({Start}-{End})",
                _availablePorts.Count, _deltaPortStart, _deltaPortEnd);
        }
        else
        {
            _logger.LogInformation("PortPool disabled (DeltaPortStart={Start}, DeltaPortEnd={End})",
                _deltaPortStart, _deltaPortEnd);
        }
    }

    /// <summary>
    ///     Allocates a delta UDP port for a player.
    ///     Each player gets a unique port (used as a nonce to match the TCP auth
    ///     session to the subsequent UDP connection). When the pool is at or
    ///     below the low-water mark, 0 is returned and the caller should reject
    ///     the player — the remaining ports are kept as a buffer.
    /// </summary>
    /// <returns>
    ///     The port number, or 0 when the pool is empty / disabled / at low water.
    /// </returns>
    public int AllocatePort(string puid)
    {
        if (!IsEnabled)
        {
            return 0;
        }

        // Low-water mark: keep the remaining ports as a buffer. Rejecting new
        // players here avoids the port-reuse race of handing out a port whose
        // previous socket is still draining.
        if (_availablePorts.Count <= _deltaLowWaterMark)
        {
            _logger.LogWarning(
                "PortPool at low water mark ({Available} <= {LowWaterMark}), rejecting PUID={Puid}",
                _availablePorts.Count, _deltaLowWaterMark, puid);
            return 0;
        }

        if (!_availablePorts.TryTake(out var port))
        {
            _logger.LogWarning("PortPool exhausted for PUID={Puid}", puid);
            return 0;
        }

        var lease = new PortLease
        {
            Port = port,
            ProductUserId = puid,
            AllocatedAt = DateTime.UtcNow,
        };

        _activeLeases[port] = lease;

        // Start 5-minute timeout: if no connection arrives, return port to pool
        var cts = new CancellationTokenSource();
        _timeouts[port] = cts;
        _ = TimeoutAsync(port, cts.Token);

        _logger.LogDebug("PortPool allocated port {Port} to PUID={Puid}", port, puid);
        return port;
    }

    public void ReturnPort(int port)
    {
        if (port <= 0)
        {
            return;
        }

        // Only exclusively-allocated ports are returned. Shared/default ports
        // never existed anymore, so any port without an active lease is ignored.
        if (!_activeLeases.TryRemove(port, out _))
        {
            _logger.LogDebug("PortPool port {Port} has no active lease, not returned", port);
            return;
        }

        if (_timeouts.TryRemove(port, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        // Mark the port as "draining": it is not put back into the available
        // pool yet. The old UDP socket still holds the port until the listener
        // is disposed; only CompletePortReturn makes it reusable, otherwise the
        // next player would race the old socket and fail to bind.
        _draining[port] = 0;

        _logger.LogInformation("PortPool port {Port} returned (draining)", port);

        OnPortReturned?.Invoke(port);
    }

    /// <summary>
    ///     Called by the matchmaker after the delta UDP listener on the port has
    ///     been fully disposed. Only now does the port become available again.
    /// </summary>
    public void CompletePortReturn(int port)
    {
        if (port <= 0)
        {
            return;
        }

        if (_draining.TryRemove(port, out _))
        {
            _availablePorts.Add(port);
            _logger.LogInformation("PortPool port {Port} fully returned and reusable", port);
        }
    }

    /// <summary>
    ///     Cancels the 5-minute allocation timeout so the port stays allocated
    ///     while the player is connected.
    /// </summary>
    public void ConfirmPort(int port)
    {
        if (port <= 0)
        {
            return;
        }

        if (_timeouts.TryRemove(port, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogDebug("PortPool port {Port} confirmed (timeout cancelled)", port);
        }
    }

    public bool HasLease(int port)
    {
        return _activeLeases.ContainsKey(port);
    }

    private async Task TimeoutAsync(int port, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), ct);

            // Timer fired — no connection came in
            _logger.LogWarning("PortPool port {Port} lease expired (no connection), returning to pool", port);
            ReturnPort(port);
        }
        catch (OperationCanceledException)
        {
            // Normal — port was used or explicitly returned
        }
    }

    public void Dispose()
    {
        foreach (var (_, cts) in _timeouts)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _timeouts.Clear();
        _activeLeases.Clear();
    }

    private sealed class PortLease
    {
        public int Port { get; init; }
        public string ProductUserId { get; init; } = string.Empty;
        public DateTime AllocatedAt { get; init; }
    }
}

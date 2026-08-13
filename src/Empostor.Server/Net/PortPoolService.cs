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
    private readonly int _deltaPortStart;
    private readonly int _deltaPortEnd;
    private readonly int _listenPort;

    public bool IsEnabled => _deltaPortStart > 0
                             && _deltaPortEnd >= _deltaPortStart;

    /// <summary>
    ///     The port usable as a "default" UDP port right now.
    ///     - Delta disabled: the configured <see cref="ServerConfig.ListenPort" />.
    ///     - Delta enabled: only when the pool has exactly one port left, that
    ///       last port is shared as the default entry for new players; otherwise 0.
    /// </summary>
    public int DefaultPort
    {
        get
        {
            if (!IsEnabled)
            {
                return _listenPort;
            }

            return _availablePorts.Count == 1 && _availablePorts.TryPeek(out var port)
                ? port
                : 0;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the pool has only one port left and
    ///     is therefore sharing that last port across all new players. In this
    ///     mode authentication falls back to IP matching, so the server should
    ///     not hard-require a per-port auth binding.
    /// </summary>
    public bool IsSharingLastPort => IsEnabled && _availablePorts.Count <= 1;

    /// <summary>
    ///     Invoked when a port is returned to the pool (via disconnect or timeout expiry).
    ///     Subscribers should stop the delta listener and close firewall rules.
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
    ///     While the pool has at least two free ports, each player gets a unique
    ///     port. When only one port is left, it is <b>not</b> handed out — it is
    ///     shared by all new players (IP-based matching), so the server keeps a
    ///     working default port and never runs out.
    /// </summary>
    /// <returns>
    ///     The port number, or 0 when the pool is empty / disabled.
    ///     <paramref name="isShared" /> is <c>true</c> when the returned port is
    ///     the shared default port (matching must fall back to the client IP).
    /// </returns>
    public int AllocatePort(string puid, out bool isShared)
    {
        isShared = false;

        if (!IsEnabled)
        {
            return 0;
        }

        // Only one port left: keep it in the pool as the shared default entry.
        if (_availablePorts.Count <= 1)
        {
            if (_availablePorts.TryPeek(out var sharedPort))
            {
                isShared = true;
                _logger.LogInformation(
                    "PortPool sharing last port {Port} for PUID={Puid} (IP-based matching)",
                    sharedPort, puid);
                return sharedPort;
            }

            _logger.LogWarning("PortPool exhausted for PUID={Puid}", puid);
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

        // Only exclusively-allocated ports are returned to the pool. Shared
        // (default) ports stay in the pool and must never be re-added or have
        // their listener stopped while other players still use them.
        if (!_activeLeases.TryRemove(port, out _))
        {
            _logger.LogDebug("PortPool port {Port} is shared/default, not returned", port);
            return;
        }

        if (_timeouts.TryRemove(port, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _availablePorts.Add(port);

        _logger.LogInformation("PortPool returned port {Port} to pool", port);

        OnPortReturned?.Invoke(port);
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

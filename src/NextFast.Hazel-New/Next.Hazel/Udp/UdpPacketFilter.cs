using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Next.Hazel.Abstractions;
using Serilog;

namespace Next.Hazel.Udp;

/// <summary>
///     A Filtaur-style layer-7 firewall for the UDP listener.
///     It drops malformed / junk packets at line speed before they reach the
///     game server, so a simple packet-flooding attacker cannot overload the
///     server and drop everyone out of their games.
/// </summary>
/// <remarks>
///     The filter only applies to <b>new connection attempts</b> (packets from
///     unknown endpoints). Packets belonging to an established connection are
///     never rate-limited here, so in-game traffic is unaffected.
/// </remarks>
public sealed class UdpPacketFilter : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<UdpPacketFilter>();

    /// <summary>
    ///     Minimal valid packet length (send-option byte + 4-byte handshake nonce).
    /// </summary>
    public int MinPacketLength { get; set; } = 5;

    /// <summary>
    ///     Maximum accepted packet length. Anything larger is dropped instantly.
    /// </summary>
    public int MaxPacketLength { get; set; } = 1203;

    /// <summary>
    ///     Allowed burst of connection attempts from a single IP before
    ///     <see cref="BlacklistSeconds" /> kicks in.
    /// </summary>
    public int BurstLimit { get; set; } = 30;

    /// <summary>
    ///     How long an abusive IP stays blacklisted.
    /// </summary>
    public int BlacklistSeconds { get; set; } = 60;

    private readonly bool[] _validOption = BuildValidOptionTable();
    private readonly ConcurrentDictionary<IPAddress, IpState> _states = new();
    private bool _isDisposed;

    public UdpPacketFilter()
    {
    }

    /// <summary>
    ///     Fast synchronous check. Returns <c>true</c> when the packet must be
    ///     dropped, <c>false</c> when it is allowed through.
    ///     This path performs no allocations.
    /// </summary>
    public bool ShouldDrop(IPAddress ip, byte[] buffer, int length)
    {
        // 1. Size check (cheapest, no state).
        if (length < MinPacketLength || length > MaxPacketLength)
        {
            return true;
        }

        // 2. Send-option byte check (layer-7, no state).
        if (!_validOption[buffer[0]])
        {
            return true;
        }

        // 3. Per-IP connection burst limiting with a temporary blacklist.
        var now = Environment.TickCount64;
        var state = _states.GetOrAdd(ip, static _ => new IpState());

        // Reset the window when the previous window expired, and lazily shed
        // blacklisted entries whose cooldown is over.
        if (now - state.WindowStart > 1000)
        {
            if (state.BlacklistUntil > 0 && state.BlacklistUntil <= now)
            {
                _states.TryRemove(ip, out _);
                return false;
            }

            state.WindowStart = now;
            state.Count = 0;
        }

        if (state.BlacklistUntil > now)
        {
            return true;
        }

        state.Count++;
        if (state.Count <= BurstLimit)
        {
            return false;
        }

        // Abuse detected: blacklist the IP for a while.
        state.BlacklistUntil = now + BlacklistSeconds * 1000L;
        state.Count = 0;
        Logger.Warning("UdpPacketFilter blacklisted {Ip} for {Seconds}s after too many connection attempts",
            ip, BlacklistSeconds);
        return true;
    }

    private static bool[] BuildValidOptionTable()
    {
        var table = new bool[256];
        // MessageType
        table[(byte)MessageType.Unreliable] = true;
        table[(byte)MessageType.Reliable] = true;
        // UdpSendOption
        table[(byte)UdpSendOption.Hello] = true;
        table[(byte)UdpSendOption.Ping] = true;
        table[(byte)UdpSendOption.Disconnect] = true;
        table[(byte)UdpSendOption.Acknowledgement] = true;
        table[(byte)UdpSendOption.Fragment] = true;
        return table;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _states.Clear();
    }

    private sealed class IpState
    {
        public int Count;
        public long WindowStart;
        public long BlacklistUntil;
    }
}

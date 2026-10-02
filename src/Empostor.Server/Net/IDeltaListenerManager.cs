using System.Threading.Tasks;

namespace Empostor.Server.Net;

/// <summary>
///     Public interface for managing dynamically allocated UDP listeners on delta ports.
///     Implemented by Matchmaker; injected into HTTP controllers that need to start
///     listeners after port allocation.
/// </summary>
public interface IDeltaListenerManager
{
    /// <summary>
    ///     Starts a UDP listener on a specific port.
    /// </summary>
    ValueTask<bool> StartDeltaListenerAsync(int port);

    /// <summary>
    ///     Stops and disposes the UDP listener on a specific port.
    /// </summary>
    ValueTask StopDeltaListenerAsync(int port);

    /// <summary>
    ///     Returns the single shared UDP port that rate-limited (over-quota) join
    ///     requests are pointed at, binding it on first use. Its listener never
    ///     creates a player, it only sends the localized "too frequent" hint, so
    ///     an over-quota IP cannot consume a pool port per request.
    /// </summary>
    /// <returns>The reject port, or 0 when the delta pool is unavailable.</returns>
    ValueTask<int> GetRateLimitRejectPortAsync();
}

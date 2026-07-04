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
    ValueTask StartDeltaListenerAsync(int port);

    /// <summary>
    ///     Stops and disposes the UDP listener on a specific port.
    /// </summary>
    ValueTask StopDeltaListenerAsync(int port);
}

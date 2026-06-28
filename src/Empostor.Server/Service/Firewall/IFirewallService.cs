using System.Threading;
using System.Threading.Tasks;

namespace Empostor.Server.Service.Firewall;

/// <summary>
///     Manages firewall port rules for dynamic UDP delta ports.
/// </summary>
public interface IFirewallService
{
    /// <summary>
    ///     Open a UDP port in the firewall.
    /// </summary>
    ValueTask OpenPortAsync(ushort port, CancellationToken ct = default);

    /// <summary>
    ///     Close a UDP port in the firewall.
    /// </summary>
    ValueTask ClosePortAsync(ushort port, CancellationToken ct = default);
}

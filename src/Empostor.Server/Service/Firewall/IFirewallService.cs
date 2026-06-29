using System.Threading;
using System.Threading.Tasks;

namespace Empostor.Server.Service.Firewall;

public interface IFirewallService
{
    ValueTask OpenPortAsync(ushort port, CancellationToken ct = default);

    ValueTask ClosePortAsync(ushort port, CancellationToken ct = default);
}

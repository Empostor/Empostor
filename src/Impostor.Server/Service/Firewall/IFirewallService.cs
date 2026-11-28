using System.Threading;
using System.Threading.Tasks;

namespace Impostor.Server.Service.Firewall;

public interface IFirewallService
{
    ValueTask OpenPortAsync(ushort port, CancellationToken ct = default, string protocol = "udp");

    ValueTask ClosePortAsync(ushort port, CancellationToken ct = default);
}

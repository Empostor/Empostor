using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Firewall;

/// <summary>
///     No-op backend used when neither UFW nor firewalld is enabled.
/// </summary>
public sealed class NoopFirewallService : IFirewallService
{
    private readonly ILogger<NoopFirewallService> _logger;

    public NoopFirewallService(ILogger<NoopFirewallService> logger)
    {
        _logger = logger;
    }

    public ValueTask OpenPortAsync(ushort port, CancellationToken ct = default)
    {
        _logger.LogDebug("[Firewall] Noop: would open port {Port}/udp", port);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClosePortAsync(ushort port, CancellationToken ct = default)
    {
        _logger.LogDebug("[Firewall] Noop: would close port {Port}/udp", port);
        return ValueTask.CompletedTask;
    }
}

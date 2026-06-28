using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Firewall;

/// <summary>
///     Manages UFW (Uncomplicated Firewall) rules on Linux.
///     Best-effort: failures are logged as warnings and do not throw.
/// </summary>
public sealed class UfwFirewallService : IFirewallService
{
    private readonly ILogger<UfwFirewallService> _logger;

    public UfwFirewallService(ILogger<UfwFirewallService> logger)
    {
        _logger = logger;
    }

    public async ValueTask OpenPortAsync(ushort port, CancellationToken ct = default)
    {
        await RunUfwAsync($"allow {port}/udp", ct);
    }

    public async ValueTask ClosePortAsync(ushort port, CancellationToken ct = default)
    {
        await RunUfwAsync($"delete allow {port}/udp", ct);
    }

    private async ValueTask RunUfwAsync(string arguments, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ufw",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("[Firewall] ufw {Args} failed (exit={Code}): {Error}",
                    arguments, process.ExitCode, stderr.Trim());
            }
            else
            {
                _logger.LogInformation("[Firewall] ufw {Args} succeeded", arguments);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Firewall] Failed to run ufw {Args}", arguments);
        }
    }
}

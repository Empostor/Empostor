using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Firewall;

/// <summary>
///     firewalld backend (RHEL/Fedora/CentOS).
///     Best-effort: failures are logged as warnings and do not throw.
/// </summary>
public sealed class FirewalldFirewallService : IFirewallService
{
    private readonly ILogger<FirewalldFirewallService> _logger;

    public FirewalldFirewallService(ILogger<FirewalldFirewallService> logger)
    {
        _logger = logger;
    }

    public async ValueTask OpenPortAsync(ushort port, CancellationToken ct = default)
    {
        await RunFirewallCmdAsync($"--add-port={port}/udp --permanent", ct);
        await RunFirewallCmdAsync("--reload", ct);
    }

    public async ValueTask ClosePortAsync(ushort port, CancellationToken ct = default)
    {
        await RunFirewallCmdAsync($"--remove-port={port}/udp --permanent", ct);
        await RunFirewallCmdAsync("--reload", ct);
    }

    private async ValueTask RunFirewallCmdAsync(string arguments, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "firewall-cmd",
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
                _logger.LogWarning("Firewallfirewall-cmd {Args} failed (exit={Code}): {Error}",
                    arguments, process.ExitCode, stderr.Trim());
            }
            else
            {
                _logger.LogInformation("Firewallfirewall-cmd {Args} succeeded", arguments);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FirewallFailed to run firewall-cmd {Args}", arguments);
        }
    }
}

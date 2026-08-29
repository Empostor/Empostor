using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Monitor;

[EmpostorPlugin("gg.empostor.monitor")]
public sealed class MonitorPlugin : PluginBase
{
    private readonly ILogger<MonitorPlugin> _logger;

    public MonitorPlugin(ILogger<MonitorPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Monitor] Enabled. Status API at /api/monitor/status, health at /api/monitor/health");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

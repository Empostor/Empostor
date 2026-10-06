using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Maintenance;

[EmpostorPlugin("cn.hayashiume.maintenance")]
public sealed class MaintenancePlugin : PluginBase
{
    private readonly ILogger<MaintenancePlugin> _logger;

    public MaintenancePlugin(ILogger<MaintenancePlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Maintenance] Plugin enabled. Use the admin panel (Plugins → Maintenance) to toggle maintenance mode.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

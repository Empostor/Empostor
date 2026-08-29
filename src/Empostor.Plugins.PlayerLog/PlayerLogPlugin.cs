using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.PlayerLog;

[EmpostorPlugin("gg.empostor.playerlog")]
public sealed class PlayerLogPlugin : PluginBase
{
    private readonly ILogger<PlayerLogPlugin> _logger;

    public PlayerLogPlugin(ILogger<PlayerLogPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[PlayerLog] Enabled. Player activity logs are available in the admin panel.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

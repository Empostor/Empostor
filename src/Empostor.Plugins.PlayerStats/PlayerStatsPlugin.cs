using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.PlayerStats;

[EmpostorPlugin("cn.hayashiume.playerstats", "Player Stats", "HayashiUme", "2.0.0")]
public sealed class PlayerStatsPlugin : PluginBase
{
    private readonly ILogger<PlayerStatsPlugin> _logger;

    public PlayerStatsPlugin(ILogger<PlayerStatsPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[PlayerStats] Enabled. View and reset statistics in the admin panel (Plugins → Player Stats).");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

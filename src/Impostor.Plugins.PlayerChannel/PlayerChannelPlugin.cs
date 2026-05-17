using System.Threading.Tasks;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.PlayerChannel;

[ImpostorPlugin("cn.hayashiume.playerchannel", "Player Channel", "HayashiUme", "1.0.0")]
public sealed class PlayerChannelPlugin : PluginBase
{
    private readonly ILogger<PlayerChannelPlugin> _logger;

    public PlayerChannelPlugin(ILogger<PlayerChannelPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[PlayerChannel] Enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

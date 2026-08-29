using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Privacy;

[EmpostorPlugin("gg.empostor.privacy")]
public sealed class PrivacyPlugin : PluginBase
{
    private readonly ILogger<PrivacyPlugin> _logger;

    public PrivacyPlugin(ILogger<PrivacyPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Privacy] Enabled. Privacy policy page at /privacy");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

using System.Threading.Tasks;
using Impostor.Api.Plugins;
using Impostor.Plugins.Welcome.Service;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Welcome;

[ImpostorPlugin("cn.Empostor.welcome", "Welcome Messages", "Impostor", "1.0.0")]
public sealed class WelcomePlugin : PluginBase
{
    private readonly ILogger<WelcomePlugin> _logger;
    private readonly WelcomeMessageService _svc;

    public WelcomePlugin(ILogger<WelcomePlugin> logger, WelcomeMessageService svc)
    {
        _logger = logger;
        _svc = svc;
    }

    public override ValueTask EnableAsync()
    {
        _svc.EnsureDefaults();
        _logger.LogInformation("[Welcome] Enabled. Edit Messages/HelloWorld.txt to customise.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

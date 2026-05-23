using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

[EmpostorPlugin("cn.hayashiume.narrator")]
public sealed class NarratorPlugin : PluginBase
{
    private readonly ILogger<NarratorPlugin> _logger;

    public NarratorPlugin(ILogger<NarratorPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Narrator] Plugin enabled. #narrator command registered.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

using System.Threading.Tasks;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Titles;

[ImpostorPlugin("cn.Empostor.titles", "Title System", "Empostor", "1.0.0")]
public sealed class TitlesPlugin : PluginBase
{
    private readonly ILogger<TitlesPlugin> _logger;

    public TitlesPlugin(ILogger<TitlesPlugin> logger) => _logger = logger;

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Titles] Plugin enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

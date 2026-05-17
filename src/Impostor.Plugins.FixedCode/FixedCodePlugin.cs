using System.Threading.Tasks;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.FixedCode;

[ImpostorPlugin("cn.Empostor.fixedcode", "Fixed Room Code", "Empostor", "1.0.0")]
public sealed class FixedCodePlugin : PluginBase
{
    private readonly ILogger<FixedCodePlugin> _logger;
    private readonly FixedCodeListener _listener;

    public FixedCodePlugin(ILogger<FixedCodePlugin> logger, FixedCodeListener listener)
    {
        _logger   = logger;
        _listener = listener;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[FixedCode] Plugin enabled. {Count} mapping(s) loaded.",
            _listener.MappingCount);
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Code;

[EmpostorPlugin("cn.hayashiume.code")]
public sealed class CodePlugin : PluginBase
{
    private readonly ILogger<CodePlugin> _logger;
    private readonly IGameCodeManager _manager;

    public CodePlugin(ILogger<CodePlugin> logger, IGameCodeManager manager)
    {
        _logger = logger;
        _manager = manager;
    }

    public override ValueTask EnableAsync()
    {
        var total = _manager.FourCharCodes + _manager.SixCharCodes;
        _logger.LogInformation(
            "[Code] Loaded {FourCharCodes} 4-char codes and {SixCharCodes} 6-char codes [{Total} total] from {Path}",
            _manager.FourCharCodes, _manager.SixCharCodes, total, _manager.Path);
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

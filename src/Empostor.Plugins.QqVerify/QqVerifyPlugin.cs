using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.QqVerify;

[EmpostorPlugin("cn.hayashiume.qqverify", "QQ Verify", "ELinmei", "1.0.0")]
public sealed class QqVerifyPlugin : PluginBase
{
    private readonly ILogger<QqVerifyPlugin> _logger;

    public QqVerifyPlugin(ILogger<QqVerifyPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[QQVerify] Plugin enabled. /verify command registered.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.QqVerify;

[EmpostorPlugin(Owner, "QQ Verify", "ELinmei", "1.0.0")]
public sealed class QqVerifyPlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.qqverify";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<QqVerifyPlugin> _logger;
    private IDisposable? _registration;
    public QqVerifyPlugin(ILogger<QqVerifyPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(QqVerifyPlugin).Assembly, "Empostor.Plugins.QqVerify.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

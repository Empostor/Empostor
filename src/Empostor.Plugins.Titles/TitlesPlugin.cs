using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Titles;

[EmpostorPlugin(Owner, "Title System", "Empostor", "1.1.0")]
public sealed class TitlesPlugin : PluginBase
{
    public const string Owner = "cn.Empostor.titles";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<TitlesPlugin> _logger;
    private IDisposable? _registration;
    public TitlesPlugin(ILogger<TitlesPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(TitlesPlugin).Assembly, "Empostor.Plugins.Titles.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

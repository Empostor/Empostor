using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

[EmpostorPlugin(Owner)]
public sealed class NarratorPlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.narrator";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<NarratorPlugin> _logger;
    private IDisposable? _registration;
    public NarratorPlugin(ILogger<NarratorPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(NarratorPlugin).Assembly, "Empostor.Plugin.Narrator.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.MapVote;

[EmpostorPlugin(Owner)]
public sealed class MapVotePlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.mapvote";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<MapVotePlugin> _logger;
    private IDisposable? _registration;
    public MapVotePlugin(ILogger<MapVotePlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(MapVotePlugin).Assembly, "Empostor.Plugins.MapVote.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

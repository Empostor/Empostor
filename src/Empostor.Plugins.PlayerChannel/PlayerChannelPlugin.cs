using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.PlayerChannel;

[EmpostorPlugin(Owner, "Player Channel", "HayashiUme", "1.0.0")]
public sealed class PlayerChannelPlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.playerchannel";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<PlayerChannelPlugin> _logger;
    private IDisposable? _registration;
    public PlayerChannelPlugin(ILogger<PlayerChannelPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(PlayerChannelPlugin).Assembly, "Empostor.Plugins.PlayerChannel.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

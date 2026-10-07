using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

[EmpostorPlugin(Owner, "Discord Webhook", "HayashiUme", "2.0.0")]
public sealed class DiscordWebhookPlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.discordwebhook";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<DiscordWebhookPlugin> _logger;
    private IDisposable? _registration;
    public DiscordWebhookPlugin(ILogger<DiscordWebhookPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(DiscordWebhookPlugin).Assembly, "Empostor.Plugins.DiscordWebhook.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

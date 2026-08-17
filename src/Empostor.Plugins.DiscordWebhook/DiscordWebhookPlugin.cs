using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

[EmpostorPlugin("cn.hayashiume.discordwebhook", "Discord Webhook", "HayashiUme", "2.0.0")]
public sealed class DiscordWebhookPlugin : PluginBase
{
    private readonly ILogger<DiscordWebhookPlugin> _logger;

    public DiscordWebhookPlugin(ILogger<DiscordWebhookPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[DiscordWebhook] Enabled. Configure URLs in the admin panel (Plugins → Discord Webhook).");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

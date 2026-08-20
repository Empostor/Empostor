using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookStore : JsonDataStore<DiscordWebhookConfig>
{
    private const string ConfigFile = "[Empostor.Plugins.DiscordWebhook]Config.json";

    public DiscordWebhookStore(ILogger<DiscordWebhookStore> logger)
        : base(logger, legacyPath: "discord_webhook.json")
    {
        JsonOpts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        Load();

        if (string.IsNullOrEmpty(MatchmakerUrl) && string.IsNullOrEmpty(AdminUrl))
        {
            var cfg = PluginConfigLoader.Load<DiscordWebhookConfig>(ConfigPath());
            var legacy = string.IsNullOrWhiteSpace(cfg.WebhookUrl) ? string.Empty : cfg.WebhookUrl;
            MatchmakerUrl = string.IsNullOrWhiteSpace(cfg.MatchmakerUrl) ? legacy : cfg.MatchmakerUrl;
            AdminUrl = string.IsNullOrWhiteSpace(cfg.AdminUrl) ? legacy : cfg.AdminUrl;
        }
    }

    public string MatchmakerUrl { get; set; } = string.Empty;

    public string AdminUrl { get; set; } = string.Empty;

    public DiscordWebhookConfig Snapshot => new()
    {
        MatchmakerUrl = MatchmakerUrl,
        AdminUrl = AdminUrl,
    };

    protected override DiscordWebhookConfig GetSnapshot() => Snapshot;

    protected override void ApplySnapshot(DiscordWebhookConfig data)
    {
        MatchmakerUrl = data.MatchmakerUrl;
        AdminUrl = data.AdminUrl;
    }

    public new async ValueTask SaveAsync() => await base.SaveAsync();

    private static string ConfigPath() => Path.Combine(Directory.GetCurrentDirectory(), ConfigFile);
}

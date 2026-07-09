using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Api;

public sealed class DiscordWebhookStore : JsonDataStore<DiscordWebhookConfig>
{
    public DiscordWebhookStore(ILogger<DiscordWebhookStore> logger, IOptions<DiscordWebhookConfig> config)
        : base(logger, legacyPath: "discord_webhook.json")
    {
        JsonOpts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Try loading from persisted file (handled by base.Load if file exists)
        Load();

        // If no persisted data was loaded, fall back to config.json defaults
        if (string.IsNullOrEmpty(MatchmakerUrl) && string.IsNullOrEmpty(AdminUrl))
        {
            var cfg = config.Value;
            MatchmakerUrl = cfg.MatchmakerUrl;
            AdminUrl = cfg.AdminUrl;

            // Migrate legacy WebhookUrl if present
            MigrateLegacy(cfg);
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

    private static void MigrateLegacy(DiscordWebhookConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.WebhookUrl))
        {
            if (string.IsNullOrWhiteSpace(cfg.MatchmakerUrl))
            {
                cfg.MatchmakerUrl = cfg.WebhookUrl;
            }

            if (string.IsNullOrWhiteSpace(cfg.AdminUrl))
            {
                cfg.AdminUrl = cfg.WebhookUrl;
            }
        }
    }
}

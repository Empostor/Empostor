using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Api;

public sealed class DiscordWebhookStore
{
    private static readonly string ConfigFile = Path.Combine(Directory.GetCurrentDirectory(), "discord_webhook.json");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<DiscordWebhookStore> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public DiscordWebhookStore(ILogger<DiscordWebhookStore> logger, IOptions<DiscordWebhookConfig> config)
    {
        _logger = logger;

        DiscordWebhookConfig? cfg = null;

        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = File.ReadAllText(ConfigFile);
                cfg = JsonSerializer.Deserialize<DiscordWebhookConfig>(json);
                if (cfg != null)
                {
                    _logger.LogInformation("DiscordWebhookLoaded from {File}", ConfigFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DiscordWebhookFailed to load {File}, using defaults", ConfigFile);
            }
        }

        // Fall back to config.json values
        cfg ??= config.Value;

        // Migrate legacy WebhookUrl to new fields if present and new fields are empty
        MigrateLegacy(cfg);

        MatchmakerUrl = cfg.MatchmakerUrl;
        AdminUrl = cfg.AdminUrl;
    }

    public string MatchmakerUrl { get; set; } = string.Empty;

    public string AdminUrl { get; set; } = string.Empty;

    public DiscordWebhookConfig Snapshot => new()
    {
        MatchmakerUrl = MatchmakerUrl,
        AdminUrl = AdminUrl,
    };

    public async ValueTask SaveAsync()
    {
        if (!await _saveLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(Snapshot, JsonOpts);
            await File.WriteAllTextAsync(ConfigFile, json);
            _logger.LogInformation("DiscordWebhookSaved to {File}", ConfigFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiscordWebhookFailed to save {File}", ConfigFile);
        }
        finally
        {
            _saveLock.Release();
        }
    }

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

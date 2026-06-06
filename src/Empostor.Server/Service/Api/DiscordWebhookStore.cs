using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Api;

public sealed class DiscordWebhookStore
{
    private static readonly string ConfigFile = Path.Combine(Directory.GetCurrentDirectory(), "discord_webhook.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly ILogger<DiscordWebhookStore> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public DiscordWebhookStore(ILogger<DiscordWebhookStore> logger, IOptions<DiscordWebhookConfig> config)
    {
        _logger = logger;

        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = File.ReadAllText(ConfigFile);
                var saved = JsonSerializer.Deserialize<DiscordWebhookConfig>(json);
                if (saved != null)
                {
                    Enabled = saved.Enabled;
                    WebhookUrl = saved.WebhookUrl;
                    NotifyOnGameCreated = saved.NotifyOnGameCreated;
                    NotifyOnBan = saved.NotifyOnBan;
                    NotifyOnReport = saved.NotifyOnReport;
                    NotifyOnPlayerJoin = saved.NotifyOnPlayerJoin;
                    NotifyOnGameEnded = saved.NotifyOnGameEnded;
                    _logger.LogInformation("[DiscordWebhook] Loaded from {File}", ConfigFile);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiscordWebhook] Failed to load {File}, using defaults", ConfigFile);
            }
        }

        // Fall back to config.json values
        var cfg = config.Value;
        Enabled = cfg.Enabled;
        WebhookUrl = cfg.WebhookUrl;
        NotifyOnGameCreated = cfg.NotifyOnGameCreated;
        NotifyOnBan = cfg.NotifyOnBan;
        NotifyOnReport = cfg.NotifyOnReport;
        NotifyOnPlayerJoin = cfg.NotifyOnPlayerJoin;
        NotifyOnGameEnded = cfg.NotifyOnGameEnded;
    }

    public bool Enabled { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;

    public bool NotifyOnGameCreated { get; set; } = true;

    public bool NotifyOnBan { get; set; } = true;

    public bool NotifyOnReport { get; set; } = true;

    public bool NotifyOnPlayerJoin { get; set; }

    public bool NotifyOnGameEnded { get; set; }

    public DiscordWebhookConfig Snapshot => new()
    {
        Enabled = Enabled,
        WebhookUrl = WebhookUrl,
        NotifyOnGameCreated = NotifyOnGameCreated,
        NotifyOnBan = NotifyOnBan,
        NotifyOnReport = NotifyOnReport,
        NotifyOnPlayerJoin = NotifyOnPlayerJoin,
        NotifyOnGameEnded = NotifyOnGameEnded,
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
            _logger.LogInformation("[DiscordWebhook] Saved to {File}", ConfigFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DiscordWebhook] Failed to save {File}", ConfigFile);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}

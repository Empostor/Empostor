using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Service.Api;

internal sealed class DiscordWebhookListener : IEventListener
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ILogger<DiscordWebhookListener> _logger;
    private readonly IHttpClientFactory _http;
    private readonly DiscordWebhookConfig _config;
    private readonly ConcurrentDictionary<string, DateTime> _throttle = new();

    public DiscordWebhookListener(
        ILogger<DiscordWebhookListener> logger,
        IHttpClientFactory http,
        IOptions<DiscordWebhookConfig> config)
    {
        _logger = logger;
        _http = http;
        _config = config.Value;
    }

    [EventListener]
    public ValueTask OnGameCreated(IGameCreatedEvent e)
    {
        if (!_config.Enabled || !_config.NotifyOnGameCreated)
        {
            return default;
        }

        return SendAsync("Game Created", 3066993, new()
        {
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Host"] = e.Host?.Name ?? "—",
            ["Host FC"] = e.Host?.FriendCode ?? "—",
        });
    }

    [EventListener]
    public ValueTask OnPlayerLeft(IGamePlayerLeftEvent e)
    {
        if (!_config.Enabled || !_config.NotifyOnBan || !e.IsBan)
        {
            return default;
        }

        return SendAsync("Player Banned", 15158332, new()
        {
            ["Player"] = e.Player.Client.Name,
            ["Friend Code"] = e.Player.Client.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
        });
    }

    [EventListener]
    public ValueTask OnPlayerReport(IPlayerReportEvent e)
    {
        if (!_config.Enabled || !_config.NotifyOnReport)
        {
            return default;
        }

        return SendAsync("Player Reported", 16776960, new()
        {
            ["Reporter"] = e.ClientPlayer.Client.Name,
            ["Reporter FC"] = e.ClientPlayer.Client.FriendCode ?? "—",
            ["Reported"] = e.ReportedClient?.Name ?? "body",
            ["Reported FC"] = e.ReportedClient?.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Reason"] = e.Reason.ToString(),
        });
    }

    [EventListener]
    public ValueTask OnPlayerJoin(IGamePlayerJoinedEvent e)
    {
        if (!_config.Enabled || !_config.NotifyOnPlayerJoin)
        {
            return default;
        }

        return SendAsync("Player Joined", 3066993, new()
        {
            ["Player"] = e.Player.Client.Name,
            ["Friend Code"] = e.Player.Client.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Players"] = $"{e.Game.PlayerCount}/{e.Game.Options.MaxPlayers}",
        });
    }

    [EventListener]
    public ValueTask OnGameEnded(IGameEndedEvent e)
    {
        if (!_config.Enabled || !_config.NotifyOnGameEnded)
        {
            return default;
        }

        return SendAsync("Game Ended", 10181046, new()
        {
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Result"] = e.GameOverReason.ToString(),
            ["Players"] = e.Game.PlayerCount.ToString(),
        });
    }

    private async ValueTask SendAsync(string title, int color, Dictionary<string, string> fields)
    {
        var url = _config.WebhookUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var key = title + (fields.TryGetValue("Game", out var gc) ? gc : string.Empty);
        if (_throttle.TryGetValue(key, out var last) && (DateTime.UtcNow - last).TotalSeconds < 2)
        {
            return;
        }

        _throttle[key] = DateTime.UtcNow;

        try
        {
            var embed = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        color,
                        fields = fields.Select(kv => new
                        {
                            name = kv.Key,
                            value = kv.Value,
                            inline = true,
                        }),
                        timestamp = DateTime.UtcNow.ToString("o"),
                        footer = new { text = "Empostor" },
                    },
                },
            };

            var json = JsonSerializer.Serialize(embed, JsonOpts);
            using var client = _http.CreateClient();
            var resp = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Discord] Webhook returned {Status}", (int)resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discord] Failed to send webhook");
        }
    }
}

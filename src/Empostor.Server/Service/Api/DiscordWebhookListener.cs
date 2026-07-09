using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Player;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Api;

internal sealed class DiscordWebhookListener : IEventListener
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ILogger<DiscordWebhookListener> _logger;
    private readonly IHttpClientFactory _http;
    private readonly DiscordWebhookStore _config;

    public DiscordWebhookListener(
        ILogger<DiscordWebhookListener> logger,
        IHttpClientFactory http,
        DiscordWebhookStore config)
    {
        _logger = logger;
        _http = http;
        _config = config;
    }

    [EventListener]
    public ValueTask OnGameCreated(IGameCreatedEvent e)
    {
        var url = _config.MatchmakerUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Game Created", 3066993, new()
        {
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Host"] = e.Host?.Name ?? "—",
            ["Host FC"] = e.Host?.FriendCode ?? "—",
        });
    }

    [EventListener]
    public ValueTask OnGameStarted(IGameStartedEvent e)
    {
        var url = _config.MatchmakerUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Game Started", 3447003, new()
        {
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Map"] = e.Game.Options.Map.ToString(),
            ["Players"] = e.Game.PlayerCount.ToString(),
            ["Impostors"] = e.Game.Options.NumImpostors.ToString(),
        });
    }

    [EventListener]
    public ValueTask OnPlayerJoin(IGamePlayerJoinedEvent e)
    {
        var url = _config.MatchmakerUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Player Joined", 3066993, new()
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
        var url = _config.MatchmakerUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Game Ended", 10181046, new()
        {
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Result"] = e.GameOverReason.ToString(),
            ["Players"] = e.Game.PlayerCount.ToString(),
        });
    }

    // ── Admin events ───────────────────────────────────────────────
    [EventListener]
    public ValueTask OnPlayerLeft(IGamePlayerLeftEvent e)
    {
        if (!e.IsBan)
        {
            return default;
        }

        var url = _config.AdminUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Player Banned", 15158332, new()
        {
            ["Player"] = e.Player.Client.Name,
            ["Friend Code"] = e.Player.Client.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
        });
    }

    [EventListener]
    public ValueTask OnPlayerReport(IPlayerReportEvent e)
    {
        var url = _config.AdminUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return default;
        }

        return SendAsync(url, "Player Reported", 16776960, new()
        {
            ["Reporter"] = e.ClientPlayer.Client.Name,
            ["Reporter FC"] = e.ClientPlayer.Client.FriendCode ?? "—",
            ["Reported"] = e.ReportedClient?.Name ?? "body",
            ["Reported FC"] = e.ReportedClient?.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Reason"] = e.Reason.ToString(),
        });
    }

    private async ValueTask SendAsync(string url, string title, int color, Dictionary<string, string> fields)
    {
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
                _logger.LogWarning("DiscordWebhook returned {Status}", (int)resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiscordFailed to send webhook");
        }
    }
}

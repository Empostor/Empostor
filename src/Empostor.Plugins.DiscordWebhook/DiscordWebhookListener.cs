using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookListener : IEventListener
{
    private const int ColorLobby = 3447003;
    private const int ColorInGame = 3066993;
    private const int ColorEnded = 15105570;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ILogger<DiscordWebhookListener> _logger;
    private readonly IHttpClientFactory _http;
    private readonly DiscordWebhookStore _config;
    private readonly LiveGameTracker _tracker;

    // Per-lobby edit throttle, plus a marker so skipped updates are flushed once instead of lost.
    private readonly ConcurrentDictionary<int, DateTime> _lastEdit = new();
    private readonly ConcurrentDictionary<int, byte> _flushScheduled = new();

    public DiscordWebhookListener(
        ILogger<DiscordWebhookListener> logger,
        IHttpClientFactory http,
        DiscordWebhookStore config,
        LiveGameTracker tracker)
    {
        _logger = logger;
        _http = http;
        _config = config;
        _tracker = tracker;
    }

    // ---- Live mode: one persistent message per lobby, edited as it changes ----

    [EventListener]
    public void OnGameCreated(IGameCreatedEvent e)
    {
        if (_config.LiveRoomUpdates)
        {
            // The live message is created on the first player join instead: at creation time the
            // host has not joined yet, so the embed would be born as an empty 0/15 lobby.
            return;
        }

        SendOneShot("Game Created", ColorLobby, new()
        {
            ["Host"] = e.Host?.Name ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Map"] = e.Game.Options.Map.ToString(),
            ["Players"] = $"{e.Game.PlayerCount}/{e.Game.Options.MaxPlayers}",
            ["Impostors"] = e.Game.Options.NumImpostors.ToString(),
            ["Note"] = e.Game.Note ?? "—",
        });
    }

    [EventListener]
    public void OnGameStarted(IGameStartedEvent e)
    {
        if (!_config.LiveRoomUpdates) { SendOneShot("Game Started", ColorInGame, LegacyFields(e.Game)); return; }
        _ = UpdateLobbyAsync(e.Game, force: true);
    }

    [EventListener]
    public void OnGameEnded(IGameEndedEvent e)
    {
        if (!_config.LiveRoomUpdates)
        {
            SendOneShot("Game Ended", ColorEnded, new()
            {
                ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
                ["Result"] = e.GameOverReason.ToString(),
                ["Players"] = e.Game.PlayerCount.ToString(),
            });
            return;
        }

        _ = UpdateLobbyAsync(e.Game, force: true, resultOverride: e.GameOverReason.ToString());
    }

    [EventListener]
    public void OnGameDestroyed(IGameDestroyedEvent e)
    {
        if (!_config.LiveRoomUpdates)
        {
            SendOneShot("Game Destroyed", 15158332, new()
            {
                ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
                ["Players"] = e.Game.PlayerCount.ToString(),
            });
            return;
        }

        _ = DeleteLiveMessageAsync(e.Game.Code);
    }

    [EventListener]
    public void OnPlayerJoined(IGamePlayerJoinedEvent e)
    {
        if (!_config.LiveRoomUpdates)
        {
            return;
        }

        // The first join is the host creating their own lobby entry — that is the moment the live
        // message is born, with a real host name and player count.
        if (!_tracker.TryGet(e.Game.Code, out _))
        {
            _ = CreateLiveMessageAsync(e.Game);
            return;
        }

        _ = UpdateLobbyAsync(e.Game);
    }

    [EventListener]
    public void OnPlayerLeft(IGamePlayerLeftEvent e)
    {
        if (e.IsBan) { SendAdminMessage("Player Banned", 15158332, new()
        {
            ["Player"] = e.Player.Client.Name,
            ["Friend Code"] = e.Player.Client.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
        }); }

        _ = UpdateLobbyAsync(e.Game);
    }

    [EventListener]
    public void OnHostChanged(IGameHostChangedEvent e) => _ = UpdateLobbyAsync(e.Game);

    // ---- Admin events (always one-shot messages on the admin webhook) ----

    [EventListener]
    public void OnPlayerReport(IPlayerReportEvent e)
    {
        SendAdminMessage("Player Reported", 16776960, new()
        {
            ["Reporter"] = e.ClientPlayer.Client.Name,
            ["Reporter FC"] = e.ClientPlayer.Client.FriendCode ?? "—",
            ["Reported"] = e.ReportedClient?.Name ?? "body",
            ["Reported FC"] = e.ReportedClient?.FriendCode ?? "—",
            ["Game"] = GameCodeParser.IntToGameName(e.Game.Code),
            ["Reason"] = e.Reason.ToString(),
        });
    }

    // ---- Live message lifecycle ----

    private async ValueTask CreateLiveMessageAsync(IGame game)
    {
        var url = _config.MatchmakerUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            var messageId = await PostLiveMessageAsync(url, BuildLobbyEmbed(game));
            if (messageId == null)
            {
                return;
            }

            _tracker.Track(new TrackedLobby
            {
                GameCode = game.Code,
                GameCodeName = GameCodeParser.IntToGameName(game.Code),
                WebhookUrl = url,
                MessageId = messageId,
                CreatedAt = DateTime.UtcNow,
                Game = game,
            });
            _lastEdit[game.Code] = DateTime.UtcNow;

            _logger.LogInformation(
                "DiscordWebhook live message created for {Code} (message {MessageId})",
                GameCodeParser.IntToGameName(game.Code), messageId);

            // The POST round-trip may have straddled a game start / player change; re-render once
            // shortly after creation so the message never stays on stale creation-time data.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (_tracker.TryGet(game.Code, out var tracked) && tracked.Game != null)
                {
                    await UpdateLobbyAsync(tracked.Game, force: true);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiscordWebhook failed to create the live message");
        }
    }

    private async ValueTask UpdateLobbyAsync(IGame game, bool force = false, string? resultOverride = null)
    {
        if (!_config.LiveRoomUpdates || !_tracker.TryGet(game.Code, out var lobby))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var last = _lastEdit.TryGetValue(game.Code, out var previous) ? previous : DateTime.MinValue;
        if (!force && now - last < TimeSpan.FromSeconds(Math.Max(1, _config.MinUpdateIntervalSeconds)))
        {
            ScheduleFlush(game.Code);
            return;
        }

        _lastEdit[game.Code] = now;

        try
        {
            var embed = BuildLobbyEmbed(game, resultOverride);
            var gone = await PatchLiveMessageAsync(lobby, embed);
            if (gone)
            {
                // The message was deleted out-of-band (manual cleanup, webhook reset, ...);
                // stop tracking instead of hammering a 404 forever.
                _tracker.Untrack(game.Code, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiscordWebhook failed to update the live message for {Code}", lobby.GameCodeName);
        }
    }

    private async ValueTask DeleteLiveMessageAsync(int gameCode)
    {
        if (!_tracker.Untrack(gameCode, out var lobby))
        {
            return;
        }

        _lastEdit.TryRemove(gameCode, out _);

        if (!_config.DeleteOnClose)
        {
            return;
        }

        try
        {
            using var client = _http.CreateClient();
            using var response = await client.DeleteAsync($"{TrimUrl(lobby.WebhookUrl)}/messages/{lobby.MessageId}");
            _logger.LogInformation(
                "DiscordWebhook deleted the live message for {Code} ({Status})", lobby.GameCodeName, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiscordWebhook failed to delete the live message for {Code}", lobby.GameCodeName);
        }
    }

    /// <summary>Re-renders a lobby whose edit was skipped by the throttle.</summary>
    private void ScheduleFlush(int gameCode)
    {
        if (!_flushScheduled.TryAdd(gameCode, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _config.MinUpdateIntervalSeconds)) + TimeSpan.FromMilliseconds(250));
                _flushScheduled.TryRemove(gameCode, out _);
                if (_tracker.TryGet(gameCode, out var lobby) && lobby.Game != null)
                {
                    await UpdateLobbyAsync(lobby.Game, force: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DiscordWebhook deferred flush failed");
            }
        });
    }

    // ---- Discord payload / transport ----

    private static object BuildLobbyEmbed(IGame game, string? resultOverride = null)
    {
        var code = GameCodeParser.IntToGameName(game.Code);
        var (status, color) = game.GameState switch
        {
            GameStates.Started => ("In Game", ColorInGame),
            GameStates.Ended => ("Ended", ColorEnded),
            GameStates.Destroyed => ("Closed", ColorEnded),
            _ => ("In Lobby", ColorLobby),
        };

        var fields = new List<object>
        {
            new { name = "Status", value = status, inline = true },
            new { name = "Host", value = game.Host?.Client.Name ?? "—", inline = true },
            new { name = "Map", value = game.Options.Map.ToString(), inline = true },
            new { name = "Players", value = $"{game.PlayerCount}/{game.Options.MaxPlayers}", inline = true },
            new { name = "Impostors", value = game.Options.NumImpostors.ToString(), inline = true },
        };

        if (resultOverride != null)
        {
            fields.Add(new { name = "Result", value = resultOverride, inline = false });
        }

        return new
        {
            embeds = new[]
            {
                new
                {
                    title = $"Lobby {code}",
                    color,
                    fields,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    footer = new { text = "Empostor" },
                },
            },
        };
    }

    private async ValueTask<string?> PostLiveMessageAsync(string url, object embed)
    {
        var json = JsonSerializer.Serialize(embed, JsonOpts);
        using var client = _http.CreateClient();
        using var response = await client.PostAsync(
            TrimUrl(url) + (url.Contains('?') ? "&" : "?") + "wait=true",
            new StringContent(json, Encoding.UTF8, "application/json"));

        // Read the body regardless of status so failures carry diagnostic content
        // (proxies, captive portals and WAFs answer with arbitrary non-JSON text).
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "DiscordWebhook live message create returned {Status}: {Body}",
                (int)response.StatusCode, Truncate(body));
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            // A 2xx response that is not JSON did not come from Discord. Logging the body
            // makes the culprit (proxy, hijacked DNS, wrong URL) immediately visible.
            _logger.LogError(
                ex,
                "DiscordWebhook live message create returned non-JSON body ({Bytes} bytes): {Body}",
                body.Length, Truncate(body));
            return null;
        }
    }

    private static string Truncate(string s)
        => s.Length <= 300 ? s : s.Substring(0, 300) + "…";

    /// <summary>Returns true when the message no longer exists (404).</summary>
    private async ValueTask<bool> PatchLiveMessageAsync(TrackedLobby lobby, object embed)
    {
        var json = JsonSerializer.Serialize(embed, JsonOpts);
        using var client = _http.CreateClient();
        using var response = await client.PatchAsync(
            $"{TrimUrl(lobby.WebhookUrl)}/messages/{lobby.MessageId}",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return true;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DiscordWebhook live message update returned {Status}", (int)response.StatusCode);
        }

        return false;
    }

    // ---- Shared helpers ----

    private static Dictionary<string, string> LegacyFields(IGame game) => new()
    {
        ["Game"] = GameCodeParser.IntToGameName(game.Code),
        ["Map"] = game.Options.Map.ToString(),
        ["Players"] = game.PlayerCount.ToString(),
        ["Impostors"] = game.Options.NumImpostors.ToString(),
    };

    private static string TrimUrl(string url) => url.TrimEnd('/');

    private void SendOneShot(string title, int color, Dictionary<string, string> fields)
        => _ = SendAsync(_config.MatchmakerUrl, title, color, fields);

    private void SendAdminMessage(string title, int color, Dictionary<string, string> fields)
        => _ = SendAsync(_config.AdminUrl, title, color, fields);

    private async ValueTask SendAsync(string url, string title, int color, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

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

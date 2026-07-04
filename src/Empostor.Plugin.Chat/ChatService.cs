using System;
using System.IO;
using System.Text.Json;
using Empostor.Api.Events.Player;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Chat;

public sealed class ChatService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly ILogger<ChatService> _logger;
    private readonly ChatConfig _config;

    public ChatService(ILogger<ChatService> logger)
    {
        _logger = logger;
        _config = LoadConfig();
    }

    public void HandleChatMessage(IPlayerChatEvent e)
    {
        var playerName = e.ClientPlayer.Client.Name;
        var slot = e.ClientPlayer.Character?.PlayerId ?? 0;

        string channelName;
        if (e.IsCancelled)
        {
            channelName = "Canceled";
        }
        else if (!e.SendToAllPlayers)
        {
            channelName = "Command";
        }
        else
        {
            channelName = "Public";
        }

        _logger.LogInformation(
            "✉ {Name} [{Slot}] → {ChannelName}: {Message}",
            playerName, slot, channelName, e.Message);

        var isHost = e.ClientPlayer.IsHost;
        var maxLength = isHost ? _config.HostMaxMessageLength : _config.PlayerMaxMessageLength;

        if (e.Message.Length > maxLength)
        {
            _logger.LogWarning(
                "✉ {PlayerName} | {PlayerType} | blocked: {Length}/{MaxLength} chars",
                playerName, isHost ? "host" : "player", e.Message.Length, maxLength);

            e.PlayerControl.SendChatToPlayerAsync(_config.TooLongMessage, e.PlayerControl);
            e.IsCancelled = true;
        }
    }

    private static ChatConfig LoadConfig()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "boot_chat.json");
        if (!File.Exists(path))
        {
            var defaults = new ChatConfig();
            var json = JsonSerializer.Serialize(new { Chat = defaults }, JsonOpts);
            File.WriteAllText(path, json);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Chat", out var chatEl))
            {
                var cfg = JsonSerializer.Deserialize<ChatConfig>(chatEl.GetRawText());
                if (cfg != null)
                    return cfg;
            }
        }
        catch (JsonException) { }

        return new ChatConfig();
    }
}

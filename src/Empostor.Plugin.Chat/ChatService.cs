using System;
using System.IO;
using System.Text.Json;
using Empostor.Api;
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
        var client = e.ClientPlayer.Client;
        var clientId = client.Id;
        var playerName = client.Name;
        var slot = e.ClientPlayer.Character?.PlayerId ?? 0;
        var gameCode = Api.Innersloth.GameCodeParser.IntToGameName(e.Game.Code);

        int channel;
        string channelName;
        if (e.IsCancelled)
        {
            channel = 0;
            channelName = "Canceled";
        }
        else if (!e.SendToAllPlayers)
        {
            channel = 255;
            channelName = "Command";
        }
        else
        {
            channel = -1;
            channelName = "Public";
        }

        _logger.LogInformation(
            "{Code} - [{ClientId}][{Name}][{Slot}] => [{Channel}]{ChannelName}: [{Message}]",
            gameCode, clientId, playerName, slot, channel, channelName, e.Message);

        var isHost = e.ClientPlayer.IsHost;
        var maxLength = isHost ? _config.HostMaxMessageLength : _config.PlayerMaxMessageLength;

        if (e.Message.Length > maxLength)
        {
            _logger.LogWarning(
                "Cancelling chat message from {PlayerName} ({PlayerType}) of {Length} chars (max: {MaxLength}): too long",
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

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
        var playerInfo = e.PlayerControl.PlayerInfo;
        var playerName = playerInfo?.PlayerName ?? "?";
        var ownerId = e.PlayerControl.OwnerId;

        if (e.ClientPlayer.Character == e.PlayerControl)
        {
            _logger.LogInformation(
                "[{GameCode}] {PlayerName} ({PlayerId}): {Message}",
                e.Game.Code, playerName, ownerId, e.Message);
        }
        else
        {
            var senderName = e.ClientPlayer.Character?.PlayerInfo?.PlayerName ?? "?";
            var senderId = e.ClientPlayer.Client.Id;
            _logger.LogInformation(
                "[{GameCode}] {SenderName} ({SenderId}) on behalf of {PlayerName} ({PlayerId}): {Message}",
                e.Game.Code, senderName, senderId, playerName, ownerId, e.Message);
        }

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

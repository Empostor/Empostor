using System;
using Empostor.Api.Events.Player;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Chat;

public sealed class ChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly ChatConfig _config;
    private readonly ChatStore _store;

    public ChatService(ILogger<ChatService> logger, ChatStore store)
    {
        _logger = logger;
        _config = ChatConfig.Load();
        _store = store;
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
        var blocked = false;

        if (e.Message.Length > maxLength)
        {
            _logger.LogWarning(
                "✉ {PlayerName} | {PlayerType} | blocked: {Length}/{MaxLength} chars",
                playerName, isHost ? "host" : "player", e.Message.Length, maxLength);

            e.PlayerControl.SendChatToPlayerAsync(_config.TooLongMessage, e.PlayerControl);
            e.IsCancelled = true;
            blocked = true;
        }

        // Feed the admin panel's Chat Monitor. Off by default only if an operator opts out.
        if (_config.CaptureMessages)
        {
            _store.Add(new ChatEntry(
                DateTime.UtcNow,
                e.Game != null ? GameCodeParser.IntToGameName(e.Game.Code) : "—",
                playerName,
                e.ClientPlayer.Client.FriendCode,
                e.ClientPlayer.Client.Id,
                blocked ? "Blocked" : channelName,
                e.Message,
                blocked));
        }
    }
}

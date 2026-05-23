using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Client;
using Empostor.Api.Events.Game;

namespace Empostor.Plugins.Message;

public sealed class MessageEventListener : IEventListener
{
    private readonly MessageStore _store;

    public MessageEventListener(MessageStore store)
    {
        _store = store;
    }

    [EventListener]
    public async ValueTask OnClientConnected(IClientConnectedEvent e)
    {
        await DeliverMessages(e.Client.FriendCode, e.Client.Language, null);
    }

    [EventListener]
    public async ValueTask OnPlayerJoined(IGamePlayerJoinedEvent e)
    {
        var fc = e.Player.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;

        // Deliver any undelivered messages when player joins a game
        var messages = _store.TakeAll(fc);
        if (messages.Count == 0) return;

        var ctrl = e.Player.Character;
        if (ctrl == null) return;

        await DeliverToPlayer(messages, ctrl, e.Player.Client.Name ?? "Player");
    }

    private async ValueTask DeliverMessages(string? friendCode, Api.Languages.Language language, Api.Innersloth.IInnerPlayerControl? ctrl)
    {
        if (string.IsNullOrEmpty(friendCode)) return;

        var messages = _store.TakeAll(friendCode);
        if (messages.Count == 0) return;

        // Messages were already delivered via OnPlayerJoined with character reference
        // OnClientConnected happens before player has a character, so store them back and wait for game join
        foreach (var msg in messages)
            _store.Add(msg);
    }

    private async ValueTask DeliverToPlayer(
        System.Collections.Generic.IReadOnlyList<PendingMessage> messages,
        Empostor.Api.Innersloth.IInnerPlayerControl ctrl,
        string playerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("--- You have {0} pending message(s) ---", messages.Count));

        foreach (var msg in messages.OrderBy(m => m.Timestamp))
        {
            var time = msg.Timestamp.ToLocalTime().ToString("MM-dd HH:mm");
            sb.AppendLine($"[{time}] <{msg.SenderName}> {msg.Content}");
        }

        await ctrl.SendChatToPlayerAsync(sb.ToString(), ctrl);
    }
}

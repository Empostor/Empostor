using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Game.Player;
using Empostor.Api.Events.Player;

namespace Empostor.Plugins.Message;

public sealed class MessageEventListener : IEventListener
{
    private readonly MessageStore _store;

    public MessageEventListener(MessageStore store)
    {
        _store = store;
    }

    [EventListener]
    public async ValueTask OnPlayerReady(IPlayerReadyEvent e)
    {
        var fc = e.ClientPlayer.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;

        var messages = _store.TakeAll(fc);
        if (messages.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine(string.Format("--- You have {0} pending message(s) ---", messages.Count));

        foreach (var msg in messages.OrderBy(m => m.Timestamp))
        {
            var time = msg.Timestamp.ToLocalTime().ToString("MM-dd HH:mm");
            sb.AppendLine($"[{time}] <{msg.SenderName}> {msg.Content}");
        }

        await e.PlayerControl.SendChatToPlayerAsync(sb.ToString(), e.PlayerControl);
    }
}

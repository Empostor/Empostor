using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Events;

namespace Empostor.Plugins.Message;

public sealed class MessageEventListener : IEventListener
{
    private readonly MessageStore _store;

    public MessageEventListener(MessageStore store)
    {
        _store = store;
    }

    [EventListener]
    public async ValueTask OnPlayerJoined(IGamePlayerJoinedEvent e)
    {
        var fc = e.Player.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;

        var messages = _store.TakeAll(fc);
        if (messages.Count == 0) return;

        var ctrl = e.Player.Character;
        if (ctrl == null) return;

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

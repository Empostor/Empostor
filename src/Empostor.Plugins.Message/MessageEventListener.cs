using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Game.Player;
using Empostor.Api.Events.Player;
using Empostor.Api.Localization;

namespace Empostor.Plugins.Message;

public sealed class MessageEventListener : IEventListener
{
    private readonly MessageStore _store;
    private readonly ILocalizationService _localization;

    public MessageEventListener(MessageStore store, ILocalizationService localization)
    {
        _store = store;
        _localization = localization;
    }

    [EventListener]
    public async ValueTask OnPlayerReady(IPlayerReadyEvent e)
    {
        var fc = e.ClientPlayer.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;

        var messages = _store.TakeAll(fc);
        if (messages.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine(_localization.Get(new LocalizedMessageKey(MessagePlugin.Owner, "message.delivery_header"), e.ClientPlayer.Client.Language, messages.Count));

        foreach (var msg in messages.OrderBy(m => m.Timestamp))
        {
            var time = msg.Timestamp.ToLocalTime().ToString("MM-dd HH:mm");
            sb.AppendLine(_localization.Get(new LocalizedMessageKey(MessagePlugin.Owner, "message.delivery_entry"), e.ClientPlayer.Client.Language,
                time, msg.SenderName, "", msg.Content));
        }

        await e.PlayerControl.SendChatToPlayerAsync(sb.ToString(), e.PlayerControl);
    }
}

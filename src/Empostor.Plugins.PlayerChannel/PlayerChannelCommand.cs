using System;
using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Plugins.PlayerChannel;

public sealed class PlayerChannelCommand : ICommand
{
    private readonly PlayerChannelConfig _config;

    public PlayerChannelCommand(PlayerChannelConfig config)
    {
        _config = config;
    }

    public string Name => "channel";

    public string Description => "Send a message to your player channel.";

    public string Usage => "channel <message>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        var text = ctx.RawArgs;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var senderFc = ctx.Sender.Client.FriendCode;
        if (string.IsNullOrEmpty(senderFc))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "playerchannel.unknown_friendcode", "[Refuse Channel] Unknown Friendcode"),
                ctx.PlayerControl);
            return true;
        }

        if (!TryGetChannel(senderFc, out var channel))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "playerchannel.not_in_channel", "[Refuse Channel] Not in any channel"),
                ctx.PlayerControl);
            return true;
        }

        var prefixed = T(ctx, "playerchannel.message_format", "[{0}] {1}")
            .Replace("{0}", channel.Name)
            .Replace("{1}", text);

        foreach (var player in ctx.Game.Players)
        {
            var targetCtrl = player.Character;
            if (targetCtrl == null) continue;

            await ctx.PlayerControl.SendChatToPlayerAsync(prefixed, targetCtrl);
        }

        return true;
    }

    private bool TryGetChannel(string friendCode, out ChannelEntry channel)
    {
        channel = null!;
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        foreach (var c in _config.Channels)
        {
            foreach (var fc in c.FriendCodes)
            {
                if (!string.IsNullOrWhiteSpace(fc) && string.Equals(fc, friendCode, StringComparison.OrdinalIgnoreCase))
                {
                    channel = c;
                    return true;
                }
            }
        }

        return false;
    }

    private static string T(CommandContext ctx, string key, string defaultText)
    {
        string result = ctx.Lang.Get(key, ctx.SenderLanguage);
        return result == key ? defaultText : result;
    }
}

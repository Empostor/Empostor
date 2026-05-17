using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api.Commands;

namespace Impostor.Plugins.PlayerChannel;

public sealed class PlayerChannelCommand : ICommand
{
    private readonly Dictionary<string, ChannelEntry> _fcToChannel;

    public PlayerChannelCommand(PlayerChannelConfig config)
    {
        _fcToChannel = new Dictionary<string, ChannelEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in config.Channels)
        {
            foreach (var fc in channel.FriendCodes)
            {
                if (!string.IsNullOrWhiteSpace(fc))
                    _fcToChannel[fc] = channel;
            }
        }
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
                "[Refuse Channel] Unnkown Friendcode", ctx.PlayerControl);
            return true;
        }

        if (!_fcToChannel.TryGetValue(senderFc, out var channel))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                "[Refuse Channel] No exsit in any Channnel", ctx.PlayerControl);
            return true;
        }

        var prefixed = $"[{channel.Name}] {text}";

        foreach (var player in ctx.Game.Players)
        {
            var fc = player.Client.FriendCode;
            if (string.IsNullOrEmpty(fc)) continue;
            if (!channel.FriendCodes.Contains(fc)) continue;

            var targetCtrl = player.Character;
            if (targetCtrl == null) continue;

            await ctx.PlayerControl.SendChatToPlayerAsync(prefixed, targetCtrl);
        }

        return true;
    }
}

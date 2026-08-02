using System.Linq;
using System.Threading.Tasks;
using Empostor.Api.Commands;
using Empostor.Api.Net;
using Next.Hazel;

namespace Empostor.Server.Commands.Commands;

public sealed class KickCommand : ICommand
{
    public string Name => "kick";

    public string Description => "Kick a player by friend code or name. Host only.";

    public string Usage => "kick <player> [reason]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.kick.not_host"), ctx.PlayerControl);
            return true;
        }

        if (ctx.Args.Length == 0)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.kick.usage"), ctx.PlayerControl);
            return true;
        }

        var search = ctx.Args[0].Trim();
        var reason = ctx.Args.Length > 1
            ? string.Join(" ", ctx.Args.Skip(1))
            : "Kicked by host";

        var target = FindPlayer(ctx, search);
        if (target == null)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.kick.not_found").Format(search), ctx.PlayerControl);
            return true;
        }

        if (target.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.kick.cannot_kick_self"), ctx.PlayerControl);
            return true;
        }

        var name = target.Client.Name;
        await target.Client.DisconnectAsync(DisconnectReason.Kicked, reason);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            ctx.GetString("command.kick.kicked").Format(name, reason), ctx.PlayerControl);

        return true;
    }

    private static IClientPlayer? FindPlayer(CommandContext ctx, string search)
    {
        return ctx.Game.Players.FirstOrDefault(p =>
        {
            var fc = p.Client.FriendCode;
            if (!string.IsNullOrEmpty(fc) && fc.Equals(search, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(fc) && fc.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(p.Client.Name) && p.Client.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        });
    }
}

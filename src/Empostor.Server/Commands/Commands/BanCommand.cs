using System.Linq;
using System.Threading.Tasks;
using Empostor.Api.Commands;
using Empostor.Api.Net;
using Empostor.Server.Service.Admin.Ban;
using Next.Hazel;

namespace Empostor.Server.Commands.Commands;

public sealed class BanCommand : ICommand
{
    private readonly BanStore _banStore;

    public BanCommand(BanStore banStore)
    {
        _banStore = banStore;
    }

    public string Name => "ban";

    public string Description => "Ban a player by friend code or name. Host only.";

    public string Usage => "ban <player> [reason]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.ban.not_host"), ctx.PlayerControl);
            return true;
        }

        if (ctx.Args.Length == 0)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.ban.usage"), ctx.PlayerControl);
            return true;
        }

        var search = ctx.Args[0].Trim();
        var reason = ctx.Args.Length > 1
            ? string.Join(" ", ctx.Args.Skip(1))
            : "Banned by host";

        var target = FindPlayer(ctx, search);
        if (target == null)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.ban.not_found").Format(search), ctx.PlayerControl);
            return true;
        }

        if (target.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.ban.cannot_ban_self"), ctx.PlayerControl);
            return true;
        }

        var name = target.Client.Name;
        var fc = target.Client.FriendCode;

        // Ban by friend code if available
        if (!string.IsNullOrEmpty(fc))
        {
            _banStore.BanFriendCode(fc, reason);
        }

        // Ban by IP
        var ip = target.Client.Connection?.EndPoint?.Address;
        if (ip != null)
        {
            _banStore.BanIp(ip, reason);
        }

        await target.Client.DisconnectAsync(DisconnectReason.Banned, reason);

        var key = !string.IsNullOrEmpty(fc) ? fc : name;
        await ctx.PlayerControl.SendChatToPlayerAsync(
            ctx.GetString("command.ban.banned").Format(key, reason), ctx.PlayerControl);

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

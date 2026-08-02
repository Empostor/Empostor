using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Commands;
using Empostor.Api.Net;
using Next.Hazel;

namespace Empostor.Server.Commands.Commands;

public sealed class EndCommand : ICommand
{
    public string Name => "end";

    public string Description => "End the current game. Host only.";

    public string Usage => "end [reason]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.end.not_host"), ctx.PlayerControl);
            return true;
        }

        var reason = string.IsNullOrWhiteSpace(ctx.RawArgs)
            ? "Game ended by host"
            : ctx.RawArgs.Trim();

        var players = ctx.Game.Players.ToList();
        foreach (var p in players)
        {
            await p.Client.DisconnectAsync(DisconnectReason.Custom, reason);
        }

        return true;
    }
}

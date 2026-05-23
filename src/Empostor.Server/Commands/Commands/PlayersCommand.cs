using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Commands;
using Empostor.Api.Innersloth;

namespace Empostor.Server.Commands.Commands;

public sealed class PlayersCommand : ICommand
{
    public string Name => "players";

    public string Description => "List all online players in the lobby.";

    public string Usage => "players";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Game.GameState != GameStates.NotStarted)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.players.not_in_lobby"), ctx.PlayerControl);
            return true;
        }

        var players = ctx.Game.Players.ToList();
        var sb = new StringBuilder();
        sb.AppendLine(ctx.GetString("command.players.header").Format(players.Count));

        foreach (var p in players)
        {
            var name = p.Client.Name;
            var fc = p.Client.FriendCode ?? "???";
            var ping = p.Client.Connection?.AveragePing ?? 0;
            sb.AppendLine(ctx.GetString("command.players.entry").Format(name, fc, (int)ping));
        }

        await ctx.PlayerControl.SendChatToPlayerAsync(sb.ToString().TrimEnd(), ctx.PlayerControl);
        return true;
    }
}

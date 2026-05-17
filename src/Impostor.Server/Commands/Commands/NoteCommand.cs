using Impostor.Api.Commands;
using System.Threading.Tasks;

namespace Impostor.Server.Commands.Commands;

public sealed class NoteCommand : ICommand
{
    public string Name => "note";

    public string Description => "Host only: set a note on this game room.";

    public string Usage => "note [text | clear]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.note.host_only"), ctx.PlayerControl);
            return true;
        }

        if (ctx.Args.Length == 0)
        {
            return false;
        }

        if (string.Equals(ctx.RawArgs, "clear", System.StringComparison.OrdinalIgnoreCase))
        {
            ctx.Game.Note = null;
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.note.cleared"), ctx.PlayerControl);
            return true;
        }

        ctx.Game.Note = ctx.RawArgs;
        await ctx.PlayerControl.SendChatToPlayerAsync(
            ctx.GetString("command.note.set").Format(ctx.RawArgs), ctx.PlayerControl);
        return true;
    }
}

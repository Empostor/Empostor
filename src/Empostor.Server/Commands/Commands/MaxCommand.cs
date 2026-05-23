using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Server.Commands.Commands;

public sealed class MaxCommand : ICommand
{
    public string Name => "max";

    public string Description => "Set the maximum number of players.";

    public string Usage => "max [1-127]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.max.host_only"), ctx.PlayerControl);
            return true;
        }

        if (ctx.Args.Length == 0 || !byte.TryParse(ctx.Args[0], out var count) || count < 1 || count > 127)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.max.invalid"), ctx.PlayerControl);
            return true;
        }

        ctx.Game.Options.MaxPlayers = count;
        await ctx.Game.SyncSettingsAsync();

        var msg = ctx.GetString("command.max.set").Format(count).Get();
        if (count > 15)
            msg += "\n" + ctx.GetString("command.max.warning");

        await ctx.PlayerControl.SendChatToPlayerAsync(msg, ctx.PlayerControl);
        return true;
    }
}

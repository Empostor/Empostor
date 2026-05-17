using Impostor.Api.Commands;
using System.Threading.Tasks;
using Impostor.Api.Innersloth.Customization;

namespace Impostor.Server.Commands.Commands;

public sealed class SetColorCommand : ICommand
{
    public string Name => "setcolor";

    public string[] Aliases => new[] { "sc", "color" };

    public string Description => "Change your player color.";

    public string Usage => "setcolor [0-17]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            return false;
        }

        if (!int.TryParse(ctx.Args[0], out var colorId) || colorId < 0 || colorId > 17)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.color.invalid"), ctx.PlayerControl);
            return true;
        }

        await ctx.PlayerControl.SetColorAsync((ColorType)colorId);
        await ctx.PlayerControl.SendChatToPlayerAsync(
            ctx.GetString("command.color.set").Format((ColorType)colorId, colorId),
            ctx.PlayerControl);
        return true;
    }
}

using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Server.Commands.Commands;

public sealed class PingCommand : ICommand
{
    public string Name => "ping";

    public string Description => "Check your current latency.";

    public string Usage => "ping";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        var ping = (int)(ctx.Sender.Client.Connection?.AveragePing ?? 0);
        await ctx.PlayerControl.SendChatToPlayerAsync(
            ctx.GetString("command.ping.result").Format(ping), ctx.PlayerControl);
        return true;
    }
}

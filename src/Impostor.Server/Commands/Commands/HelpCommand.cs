using Impostor.Api.Commands;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Server.Commands.Commands;

public sealed class HelpCommand : ICommand
{
    private readonly CommandService _service;

    public HelpCommand(CommandService service) => _service = service;

    public string Name => "help";

    public string Description => "Show available commands.";

    public string Usage => "help [command]";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Args.Length > 0)
        {
            var target = _service.All.FirstOrDefault(
                c => c.Name.Equals(ctx.Args[0], System.StringComparison.OrdinalIgnoreCase)
                  || c.Aliases.Contains(ctx.Args[0], System.StringComparer.OrdinalIgnoreCase));

            if (target == null)
            {
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    ctx.GetString("command.help.unknown").Format(ctx.Args[0]),
                    ctx.PlayerControl);
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine(ctx.GetString("command.help.entry").Format(target.Name, target.Description));
            sb.AppendLine(ctx.GetString("command.usage").Format(target.Usage));
            if (target.Aliases.Length > 0)
                sb.AppendLine(ctx.GetString("command.help.aliases")
                    .Format(string.Join(", ", target.Aliases.Select(a => "/" + a))));

            await ctx.PlayerControl.SendChatToPlayerAsync(sb.ToString().TrimEnd(), ctx.PlayerControl);
            return true;
        }

        var list = new StringBuilder(ctx.GetString("command.help.list").Get() + "\n");
        foreach (var cmd in _service.All.OrderBy(c => c.Name))
        {
            list.AppendLine(ctx.GetString("command.help.entry").Format(cmd.Name, cmd.Description));
        }

        await ctx.PlayerControl.SendChatToPlayerAsync(list.ToString().TrimEnd(), ctx.PlayerControl);
        return true;
    }
}

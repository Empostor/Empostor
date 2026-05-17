using Impostor.Api.Languages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Impostor.Api.Commands;

namespace Impostor.Server.Commands;

public sealed class CommandService
{
    private readonly ILogger<CommandService> _logger;
    private readonly LanguageService _lang;
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandService(ILogger<CommandService> logger, LanguageService lang)
    {
        _logger = logger;
        _lang = lang;
    }

    public void Register(ICommand command)
    {
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    public void RegisterAll(IEnumerable<ICommand> commands)
    {
        foreach (var cmd in commands)
        {
            Register(cmd);
        }
    }

    public IReadOnlyList<ICommand> All => _commands.Values.Distinct().ToList();

    public LanguageService Lang => _lang;

    public async ValueTask TryHandleAsync(CommandContext ctx)
    {
        if (!_commands.TryGetValue(ctx.Name, out var command))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.unknown").Format(ctx.Name), ctx.PlayerControl);
            return;
        }

        try
        {
            var success = await command.ExecuteAsync(ctx);
            if (!success)
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    ctx.GetString("command.usage").Format(command.Usage), ctx.PlayerControl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Commands] Error executing /{Name}", ctx.Name);
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.error").Format(ctx.Name), ctx.PlayerControl);
        }
    }
}

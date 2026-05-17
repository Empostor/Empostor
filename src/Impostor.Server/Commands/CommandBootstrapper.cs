using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Commands;
using Impostor.Server.Commands.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Commands;

internal sealed class CommandBootstrapper : IHostedService
{
    private readonly ILogger<CommandBootstrapper> _logger;
    private readonly CommandService _commands;
    private readonly StatCommand _statCommand;
    private readonly IEnumerable<ICommand> _pluginCommands;

    public CommandBootstrapper(
        ILogger<CommandBootstrapper> logger,
        CommandService commands,
        StatCommand statCommand,
        IEnumerable<ICommand> pluginCommands)
    {
        _logger = logger;
        _commands = commands;
        _statCommand = statCommand;
        _pluginCommands = pluginCommands;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _commands.Register(new HelpCommand(_commands));
        _commands.Register(new SetColorCommand());
        _commands.Register(new NoteCommand());
        _commands.Register(_statCommand);
        _commands.RegisterAll(_pluginCommands);
        _logger.LogInformation(
            "[Commands] Registered {Count} command(s): {Names}",
            _commands.All.Count,
            string.Join(", ", System.Linq.Enumerable.Select(_commands.All, c => "/" + c.Name)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

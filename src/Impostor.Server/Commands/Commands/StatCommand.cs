using System.Threading.Tasks;
using Impostor.Api.Commands;
using Impostor.Api.Config;
using Impostor.Server.Service.Stat;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Commands.Commands;

public sealed class StatCommand : ICommand
{
    private readonly PlayerStatsStore _store;
    private readonly PlayerStatsConfig _config;

    public StatCommand(PlayerStatsStore store, IOptions<PlayerStatsConfig> config)
    {
        _store = store;
        _config = config.Value;
    }

    public string Name => "stat";

    public string[] Aliases => new[] { "stats", "mystats" };

    public string Description => "Show your game statistics.";

    public string Usage => "stat";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!_config.Enabled)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.stat.disabled"), ctx.PlayerControl);
            return true;
        }

        var fc = ctx.Sender.Client.FriendCode;
        if (string.IsNullOrEmpty(fc))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.stat.no_stats"), ctx.PlayerControl);
            return true;
        }

        var stats = _store.GetByFriendCode(fc);
        if (stats == null)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString("command.stat.no_stats"), ctx.PlayerControl);
            return true;
        }

        async Task Send(string key, params object[] args)
            => await ctx.PlayerControl.SendChatToPlayerAsync(
                ctx.GetString(key).Format(args), ctx.PlayerControl);

        await Send("command.stat.header");
        await Send("command.stat.games", stats.GamesPlayed);
        await Send("command.stat.wins", stats.Wins);
        await Send("command.stat.losses", stats.Losses);
        await Send("command.stat.impostor", stats.ImpostorWins);
        await Send("command.stat.kills", stats.Kills);
        await Send("command.stat.deaths", stats.Deaths);
        await Send("command.stat.tasks", stats.TasksCompleted);
        await Send("command.stat.exiled", stats.TimesExiled);

        return true;
    }
}

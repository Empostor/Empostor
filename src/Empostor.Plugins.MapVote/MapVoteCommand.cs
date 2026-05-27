using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Plugins.MapVote;

public sealed class MapVoteCommand : ICommand
{
    private readonly MapVoteService _service;
    private readonly MapVoteConfig _config;

    public MapVoteCommand(MapVoteService service, MapVoteConfig config)
    {
        _service = service;
        _config = config;
    }

    public string Name => "votemap";
    public string[] Aliases => new[] { "vm" };
    public string Description => "Vote for the next map.";
    public string Usage => "votemap <map>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        var gameCode = ctx.Game.Code.ToString();

        // Host control commands
        if (ctx.Args.Length > 0)
        {
            var sub = ctx.Args[0].ToLowerInvariant();

            if (sub == "public" || sub == "start")
            {
                if (!ctx.Sender.IsHost)
                {
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "mapvote.host_only", "Only the host can start a map vote."), ctx.PlayerControl);
                    return true;
                }

                _service.StartSession(gameCode);

                await ctx.PlayerControl.SendChatToPlayerAsync(
                    T(ctx, "mapvote.session_started", "Map vote session started! Players, use #votemap <map> to vote.\nMaps: Skeld, Mira, Polus, Airship, Fungle"), ctx.PlayerControl);
                return true;
            }

            if (sub == "close" || sub == "end")
            {
                if (!ctx.Sender.IsHost)
                {
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "mapvote.host_only", "Only the host can close the vote."), ctx.PlayerControl);
                    return true;
                }

                var winner = _service.GetWinner(gameCode);
                var tally = _service.TallyVotes(gameCode);
                var voterCount = _service.VoterCount(gameCode);

                await ShowWinnerToAllAsync(ctx, winner, tally, voterCount);

                _service.StopSession(gameCode);
                return true;
            }

            if (sub == "enable" || sub == "on")
            {
                if (!ctx.Sender.IsHost)
                {
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "mapvote.host_only", "Only the host can change settings."), ctx.PlayerControl);
                    return true;
                }

                _service.SetEnabled(gameCode, true);
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    T(ctx, "mapvote.host_enabled", "Map voting enabled."), ctx.PlayerControl);
                return true;
            }

            if (sub == "disable" || sub == "off")
            {
                if (!ctx.Sender.IsHost)
                {
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "mapvote.host_only", "Only the host can change settings."), ctx.PlayerControl);
                    return true;
                }

                _service.SetEnabled(gameCode, false);
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    T(ctx, "mapvote.host_disabled", "Map voting disabled."), ctx.PlayerControl);
                return true;
            }

            if (sub == "results" || sub == "result")
            {
                await ShowResultsAsync(ctx);
                return true;
            }
        }

        // Check if voting is enabled
        if (!_service.IsEnabled(gameCode) && !ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "mapvote.disabled", "Map voting is currently disabled by the host."), ctx.PlayerControl);
            return true;
        }

        // Voting requires a map argument
        if (ctx.Args.Length == 0)
        {
            await ShowUsageAsync(ctx);
            return true;
        }

        if (!_service.TryParseMap(ctx.RawArgs!, out var map))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "mapvote.unknown_map", "Unknown map.").Replace("{0}", ctx.RawArgs ?? "?"), ctx.PlayerControl);
            return true;
        }

        var playerName = ctx.Sender.Character?.PlayerInfo?.PlayerName ?? ctx.Sender.Client.Name ?? "Unknown";
        _service.CastVote(gameCode, playerName, map);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            T(ctx, "mapvote.voted", "{0} voted for {1}.")
                .Replace("{0}", playerName)
                .Replace("{1}", MapVoteService.MapDisplayName(map)),
            ctx.PlayerControl);

        return true;
    }

    private async ValueTask ShowResultsAsync(CommandContext ctx)
    {
        var gameCode = ctx.Game.Code.ToString();
        var tally = _service.TallyVotes(gameCode);

        if (tally.Count == 0)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "mapvote.no_votes", "No map votes yet."), ctx.PlayerControl);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(T(ctx, "mapvote.results_header", "=== Map Vote Results ==="));
        foreach (var (map, count) in tally)
        {
            sb.AppendLine(T(ctx, "mapvote.results_entry", "  {0}: {1} vote(s)")
                .Replace("{0}", MapVoteService.MapDisplayName(map))
                .Replace("{1}", count.ToString()));
        }

        await ctx.PlayerControl.SendChatToPlayerAsync(sb.ToString(), ctx.PlayerControl);
    }

    private static async ValueTask ShowWinnerToAllAsync(CommandContext ctx, Api.Innersloth.MapTypes winner,
        IReadOnlyDictionary<Api.Innersloth.MapTypes, int> tally, int voterCount)
    {
        var sb = new StringBuilder();
        if (tally.Count == 0)
        {
            sb.AppendLine(ctx.Lang.Get("mapvote.results_random", ctx.SenderLanguage)
                .Replace("{0}", MapVoteService.MapDisplayName(winner)));
        }
        else
        {
            var maxVotes = tally.Values.Max();
            sb.AppendLine(ctx.Lang.Get("mapvote.results_winner", ctx.SenderLanguage)
                .Replace("{0}", MapVoteService.MapDisplayName(winner))
                .Replace("{1}", maxVotes.ToString()));
        }

        foreach (var player in ctx.Game.Players)
        {
            var ctrl = player.Character;
            if (ctrl != null)
                await ctrl.SendChatToPlayerAsync(sb.ToString(), ctrl);
        }
    }

    private async ValueTask ShowUsageAsync(CommandContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine(T(ctx, "mapvote.usage", "Usage: #votemap <map>\nMaps: Skeld, Mira, Polus, Airship, Fungle\nExample: #votemap polus"));
        if (ctx.Sender.IsHost)
            sb.AppendLine(T(ctx, "mapvote.host_commands", "Host commands: #votemap public | close | enable | disable | results"));
        await ctx.PlayerControl.SendChatToPlayerAsync(sb.ToString(), ctx.PlayerControl);
    }

    private static string T(CommandContext ctx, string key, string defaultText)
    {
        string result = ctx.Lang.Get(key, ctx.SenderLanguage);
        return result == key ? defaultText : result;
    }
}

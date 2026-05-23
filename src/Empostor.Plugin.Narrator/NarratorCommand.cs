using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Commands;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorCommand : ICommand
{
    private readonly NarratorService _service;
    private readonly ILogger<NarratorCommand> _logger;

    public NarratorCommand(NarratorService service, ILogger<NarratorCommand> logger)
    {
        _service = service;
        _logger = logger;
    }

    public string Name => "narrator";

    public string[] Aliases => new[] { "nar", "n" };

    public string Description => "Ask the narrator for advice during a meeting.";

    public string Usage => "narrator <your question or statement>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        var args = ctx.Args;

        // Host commands
        if (args.Length > 0 && ctx.Sender.IsHost)
        {
            var sub = args[0].ToLowerInvariant();

            if (sub == "enable" || sub == "on")
            {
                _service.SetEnabled(ctx.Game, true);
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    T(ctx, "narrator.host_enabled", "Narrator enabled for this game."), ctx.PlayerControl);
                return true;
            }

            if (sub == "disable" || sub == "off")
            {
                _service.SetEnabled(ctx.Game, false);
                await ctx.PlayerControl.SendChatToPlayerAsync(
                    T(ctx, "narrator.host_disabled", "Narrator disabled for this game."), ctx.PlayerControl);
                return true;
            }

            if (sub == "status")
            {
                var enabled = _service.IsEnabled(ctx.Game);
                var max = _service.GetMaxUses(ctx.Game);
                var statusText = T(ctx, enabled ? "narrator.status_enabled" : "narrator.status_disabled",
                    enabled ? "enabled" : "disabled");
                var msg = T(ctx, "narrator.host_status", "Status: {0} | Max uses per player per game: {1}")
                    .Replace("{0}", statusText)
                    .Replace("{1}", max.ToString());
                await ctx.PlayerControl.SendChatToPlayerAsync(msg, ctx.PlayerControl);
                return true;
            }

            if (sub == "limit")
            {
                if (args.Length < 2 || !int.TryParse(args[1], out var limit) || limit < 0)
                {
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "narrator.host_limit_usage", "Usage: #narrator limit <number>"), ctx.PlayerControl);
                    return true;
                }

                _service.SetMaxUses(ctx.Game, limit);
                if (limit == 0)
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "narrator.host_limit_zero", "Limit set to 0 (effectively disabled)."), ctx.PlayerControl);
                else
                    await ctx.PlayerControl.SendChatToPlayerAsync(
                        T(ctx, "narrator.host_limit_set", "Limit set to {0} per player per game.")
                            .Replace("{0}", limit.ToString()),
                        ctx.PlayerControl);
                return true;
            }
        }

        // Show host commands if no args provided
        if (args.Length == 0)
        {
            await ShowUsageAsync(ctx);
            return true;
        }

        // Check game state — meeting must be active
        if (!_service.IsMeetingActive(ctx.Game))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "narrator.meeting_only", "You can only use #narrator during a meeting."), ctx.PlayerControl);
            return true;
        }

        // Check if narrator is enabled
        if (!_service.IsEnabled(ctx.Game))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "narrator.disabled", "The narrator is currently disabled by the host."), ctx.PlayerControl);
            return true;
        }

        var playerName = ctx.PlayerControl.PlayerInfo.PlayerName;

        // Check usage limits
        var (allowed, error) = _service.TryUse(ctx.Game, playerName);
        if (!allowed)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                $"[Narrator] {error}", ctx.PlayerControl);
            return true;
        }

        var playerMessage = ctx.RawArgs?.Trim();
        if (string.IsNullOrEmpty(playerMessage))
        {
            await ShowUsageAsync(ctx);
            return true;
        }

        _logger.LogInformation("[Narrator] {player} asks: {message}", playerName, playerMessage);

        var context = _service.BuildContext(ctx.Game, ctx.PlayerControl);
        var reply = await _service.AskNarratorAsync(context, playerMessage);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            $"[Narrator] {reply}", ctx.PlayerControl);

        return true;
    }

    private async ValueTask ShowUsageAsync(CommandContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine(T(ctx, "narrator.usage",
            "Usage: #narrator <your question or statement>"));
        if (ctx.Sender.IsHost)
            sb.AppendLine(T(ctx, "narrator.host_commands",
                "Host commands: #narrator enable | disable | limit <number>"));
        await ctx.PlayerControl.SendChatToPlayerAsync(sb.ToString(), ctx.PlayerControl);
    }

    private static string T(CommandContext ctx, string key, string defaultText)
    {
        string result = ctx.Lang.Get(key, ctx.SenderLanguage);
        return result == key ? defaultText : result;
    }
}

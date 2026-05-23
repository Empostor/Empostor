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
        if (!_service.IsMeetingActive(ctx.Game))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                "[Narrator] You can only use this command during a meeting.", ctx.PlayerControl);
            return true;
        }

        var playerMessage = ctx.RawArgs?.Trim();
        if (string.IsNullOrEmpty(playerMessage))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                "Usage: /narrator <your question or statement>\nExample: /narrator I'm being accused but I have a visual task, how do I prove myself?",
                ctx.PlayerControl);
            return true;
        }

        _logger.LogInformation("[Narrator] {player} asks: {message}", ctx.PlayerControl.PlayerInfo.PlayerName, playerMessage);

        var context = _service.BuildContext(ctx.Game, ctx.PlayerControl);
        var reply = await _service.AskNarratorAsync(context, playerMessage);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            $"[Narrator] {reply}", ctx.PlayerControl);

        return true;
    }
}

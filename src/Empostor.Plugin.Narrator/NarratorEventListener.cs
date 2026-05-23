using Empostor.Api.Events;
using Empostor.Api.Events.Player;
using Empostor.Api.Events.Meeting;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorEventListener : IEventListener
{
    private readonly NarratorService _service;
    private readonly ILogger<NarratorEventListener> _logger;

    public NarratorEventListener(NarratorService service, ILogger<NarratorEventListener> logger)
    {
        _service = service;
        _logger = logger;
    }

    [EventListener]
    public void OnGameStarted(IGameStartedEvent e)
    {
        _service.RecordGameStart(e.Game);
        _logger.LogDebug("[Narrator] Game started: {code}", e.Game.Code);
    }

    [EventListener]
    public void OnGameEnded(IGameEndedEvent e)
    {
        _service.RecordGameEnd(e.Game);
        _logger.LogDebug("[Narrator] Game ended: {code}", e.Game.Code);
    }

    [EventListener]
    public void OnPlayerCompletedTask(IPlayerCompletedTaskEvent e)
    {
        _service.RecordTask(e);
    }

    [EventListener]
    public void OnPlayerMurder(IPlayerMurderEvent e)
    {
        _service.RecordMurder(e);
    }

    [EventListener]
    public void OnPlayerExile(IPlayerExileEvent e)
    {
        _service.RecordExile(e);
    }

    [EventListener]
    public void OnPlayerReport(IPlayerReportEvent e)
    {
        _service.RecordReport(e);
    }

    [EventListener]
    public void OnMeetingStarted(IMeetingStartedEvent e)
    {
        _service.RecordMeetingStart(e);
        _logger.LogDebug("[Narrator] Meeting started");
    }

    [EventListener]
    public void OnMeetingEnded(IMeetingEndedEvent e)
    {
        _service.RecordMeetingEnd(e);
        _logger.LogDebug("[Narrator] Meeting ended");
    }

    [EventListener]
    public void OnPlayerChat(IPlayerChatEvent e)
    {
        _service.RecordChat(e);
    }

    [EventListener]
    public void OnPlayerVoted(IPlayerVotedEvent e)
    {
        _service.RecordVote(e);
    }
}

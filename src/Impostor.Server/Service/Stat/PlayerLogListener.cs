using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;

namespace Impostor.Server.Service.Stat;

internal sealed class PlayerLogListener : IEventListener
{
    private readonly PlayerLogStore _store;

    public PlayerLogListener(PlayerLogStore store) => _store = store;

    private static string? Gc(IGameEvent e) => e.Game != null ? GameCodeParser.IntToGameName(e.Game.Code) : null;

    private static string? Pn(IPlayerEvent e) => e.ClientPlayer?.Client?.Name;

    private static string? Pf(IPlayerEvent e) => e.ClientPlayer?.Client?.FriendCode;

    private static int? Pid(IPlayerEvent e) => e.ClientPlayer?.Client?.Id;

    // Chat
    [EventListener]
    public void OnChat(IPlayerChatEvent e) =>
        _store.Add("Chat", Pid(e), Pn(e), Pf(e), Gc(e), e.Message);

    // RPC-like events
    [EventListener]
    public void OnReport(IPlayerReportEvent e) =>
        _store.Add("Report", Pid(e), Pn(e), Pf(e), Gc(e), $"Reported: {e.ReportedClient?.Name ?? "body"}");

    [EventListener]
    public void OnMurder(IPlayerMurderEvent e) =>
        _store.Add("Murder", Pid(e), Pn(e), Pf(e), Gc(e), $"Victim: {e.Victim?.PlayerInfo?.PlayerName ?? "unknown"}");

    [EventListener]
    public void OnExile(IPlayerExileEvent e) =>
        _store.Add("Exile", Pid(e), Pn(e), Pf(e), Gc(e), "Ejected");

    [EventListener]
    public void OnVote(IPlayerVotedEvent e) =>
        _store.Add("Vote", Pid(e), Pn(e), Pf(e), Gc(e), $"Voted: {e.VotedFor?.PlayerInfo?.PlayerName ?? "skipped"}");

    [EventListener]
    public void OnTask(IPlayerCompletedTaskEvent e) =>
        _store.Add("Task", Pid(e), Pn(e), Pf(e), Gc(e), null);

    [EventListener]
    public void OnEnterVent(IPlayerEnterVentEvent e) =>
        _store.Add("Vent", Pid(e), Pn(e), Pf(e), Gc(e), "Enter vent");

    [EventListener]
    public void OnExitVent(IPlayerExitVentEvent e) =>
        _store.Add("Vent", Pid(e), Pn(e), Pf(e), Gc(e), "Exit vent");

    // Meeting events (game-level, not player-level)
    [EventListener]
    public void OnMeetingStarted(IMeetingStartedEvent e) =>
        _store.Add("Meeting", null, null, null, Gc(e), "Meeting started");

    [EventListener]
    public void OnMeetingEnded(IMeetingEndedEvent e) =>
        _store.Add("Meeting", null, null, null, Gc(e), "Meeting ended");

    // Connection events
    [EventListener]
    public void OnClientConnected(IClientConnectedEvent e) =>
        _store.Add("Connect", e.Client.Id, e.Client.Name, e.Client.FriendCode, null, null);

    // Game lifecycle
    [EventListener]
    public void OnGameCreated(IGameCreatedEvent e) =>
        _store.Add("Game", null, null, null, Gc(e), "Game created");

    [EventListener]
    public void OnGameStarted(IGameStartedEvent e) =>
        _store.Add("Game", null, null, null, Gc(e), "Game started");

    [EventListener]
    public void OnGameEnded(IGameEndedEvent e) =>
        _store.Add("Game", null, null, null, Gc(e), $"Game ended: {e.GameOverReason}");

    [EventListener]
    public void OnGameDestroyed(IGameDestroyedEvent e) =>
        _store.Add("Game", null, null, null, Gc(e), "Game destroyed");

    // Player join/leave
    [EventListener]
    public void OnPlayerJoined(IGamePlayerJoinedEvent e) =>
        _store.Add("Join", e.Player.Client.Id, e.Player.Client.Name, e.Player.Client.FriendCode, Gc(e), null);

    [EventListener]
    public void OnPlayerLeft(IGamePlayerLeftEvent e) =>
        _store.Add("Leave", e.Player.Client.Id, e.Player.Client.Name, e.Player.Client.FriendCode, Gc(e), e.IsBan ? "Banned" : "Left");
}

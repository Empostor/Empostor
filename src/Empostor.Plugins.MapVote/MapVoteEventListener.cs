using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Game;

namespace Empostor.Plugins.MapVote;

public sealed class MapVoteEventListener : IEventListener
{
    private readonly MapVoteService _service;
    private readonly MapVoteConfig _config;

    public MapVoteEventListener(MapVoteService service, MapVoteConfig config)
    {
        _service = service;
        _config = config;
    }

    [EventListener]
    public async ValueTask OnGameStarting(IGameStartingEvent e)
    {
        if (!_config.Enabled) return;

        var gameCode = e.Game.Code.ToString();

        if (!_service.IsEnabled(gameCode)) return;

        var winner = _service.GetWinner(gameCode);

        // If host set a specific map and override is allowed, respect it when no votes cast
        if (_config.AllowHostOverride && e.Game.Options.Map != winner)
        {
            var tally = _service.TallyVotes(gameCode);
            if (tally.Count == 0)
            {
                _service.ResetVotes(gameCode);
                return;
            }
        }

        e.Game.Options.Map = winner;
        await e.Game.SyncSettingsAsync();

        // Notify players
        foreach (var player in e.Game.Players)
        {
            var ctrl = player.Character;
            if (ctrl == null) continue;
            await ctrl.SendChatToPlayerAsync(
                $"Map set to {MapVoteService.MapDisplayName(winner)} by vote.", ctrl);
        }

        _service.ResetVotes(gameCode);
    }

    [EventListener]
    public void OnGameDestroyed(IGameDestroyedEvent e)
    {
        _service.Remove(e.Game.Code.ToString());
    }
}

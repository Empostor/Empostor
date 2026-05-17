using System.Linq;
using Impostor.Api.Config;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net.Inner.Objects;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Service.Stat;

internal sealed class PlayerStatsListener : IEventListener
{
    private readonly PlayerStatsConfig _config;
    private readonly PlayerStatsStore _store;

    public PlayerStatsListener(IOptions<PlayerStatsConfig> config, PlayerStatsStore store)
    {
        _config = config.Value;
        _store = store;
    }

    [EventListener]
    public void OnPlayerMurder(IPlayerMurderEvent e)
    {
        if (!_config.Enabled)
        {
            return;
        }

        var killerFc = e.ClientPlayer.Client.FriendCode;
        if (killerFc != null)
        {
            _store.RecordKill(killerFc);
            _store.GetOrCreate(killerFc, e.ClientPlayer.Client.Name);
        }

        var victimFc = GetFriendCode(e.Game, e.Victim);
        if (victimFc != null)
        {
            _store.RecordDeath(victimFc);
        }
    }

    [EventListener]
    public void OnPlayerCompletedTask(IPlayerCompletedTaskEvent e)
    {
        if (!_config.Enabled)
        {
            return;
        }

        var fc = e.ClientPlayer.Client.FriendCode;
        if (fc != null)
        {
            _store.RecordTaskCompleted(fc);
            _store.GetOrCreate(fc, e.ClientPlayer.Client.Name);
        }
    }

    [EventListener]
    public void OnPlayerExile(IPlayerExileEvent e)
    {
        if (!_config.Enabled)
        {
            return;
        }

        var fc = e.ClientPlayer.Client.FriendCode;
        if (fc != null)
        {
            _store.RecordExile(fc);
            _store.GetOrCreate(fc, e.ClientPlayer.Client.Name);
        }
    }

    [EventListener]
    public void OnGameEnded(IGameEndedEvent e)
    {
        if (!_config.Enabled)
        {
            return;
        }

        var reason = e.GameOverReason.ToString();
        var isCrewmateWin = reason.StartsWith("Crewmates") || reason == "HideAndSeek_CrewmatesByTimer";

        foreach (var player in e.Game.Players)
        {
            var fc = player.Client.FriendCode;
            if (fc == null)
            {
                continue;
            }

            var wasImpostor = player.Character?.PlayerInfo?.IsImpostor ?? false;
            _store.RecordGameEnd(fc, player.Client.Name, isCrewmateWin, wasImpostor);
        }
    }

    private static string? GetFriendCode(IGame game, IInnerPlayerControl target)
    {
        var match = game.Players.FirstOrDefault(p => p.Character?.PlayerId == target.PlayerId);
        return match?.Client.FriendCode;
    }
}

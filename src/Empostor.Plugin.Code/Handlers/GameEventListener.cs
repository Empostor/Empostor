using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Plugin.Code.Handlers;

public sealed class GameEventListener : IEventListener
{
    private readonly IGameCodeManager _gameCodeManager;

    public GameEventListener(IGameCodeManager gameCodeManager)
    {
        _gameCodeManager = gameCodeManager;
    }

    [EventListener]
    public void OnGameCreated(IGameCreationEvent e)
    {
        e.GameCode = _gameCodeManager.Get();
    }

    [EventListener]
    public void OnGameDestroyed(IGameDestroyedEvent e)
    {
        _gameCodeManager.Release(e.Game.Code);
    }
}

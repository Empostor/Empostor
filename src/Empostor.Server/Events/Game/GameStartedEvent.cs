using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Server.Events
{
    public class GameStartedEvent : IGameStartedEvent
    {
        public GameStartedEvent(IGame game)
        {
            Game = game;
        }

        public IGame Game { get; }
    }
}

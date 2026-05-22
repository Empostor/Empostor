using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Server.Events
{
    public class GameStartingEvent : IGameStartingEvent
    {
        public GameStartingEvent(IGame game)
        {
            Game = game;
        }

        public IGame Game { get; }
    }
}

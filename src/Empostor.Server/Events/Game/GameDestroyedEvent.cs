using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Server.Events
{
    public class GameDestroyedEvent : IGameDestroyedEvent
    {
        public GameDestroyedEvent(IGame game)
        {
            Game = game;
        }

        public IGame Game { get; }
    }
}

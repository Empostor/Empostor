using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Server.Events
{
    public class GameEndedEvent : IGameEndedEvent
    {
        public GameEndedEvent(IGame game, GameOverReason gameOverReason)
        {
            Game = game;
            GameOverReason = gameOverReason;
        }

        public IGame Game { get; }

        public GameOverReason GameOverReason { get; }
    }
}

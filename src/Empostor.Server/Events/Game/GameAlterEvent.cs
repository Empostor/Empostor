using Empostor.Api.Events;
using Empostor.Api.Games;

namespace Empostor.Server.Events
{
    public class GameAlterEvent : IGameAlterEvent
    {
        public GameAlterEvent(IGame game, bool isPublic)
        {
            Game = game;
            IsPublic = isPublic;
        }

        public IGame Game { get; }

        public bool IsPublic { get; }
    }
}

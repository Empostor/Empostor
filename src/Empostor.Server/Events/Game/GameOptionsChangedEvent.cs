using Empostor.Api.Events;
using Empostor.Api.Games;
using static Empostor.Api.Events.IGameOptionsChangedEvent;

namespace Empostor.Server.Events
{
    public class GameOptionsChangedEvent : IGameOptionsChangedEvent
    {
        public GameOptionsChangedEvent(IGame game, ChangeReason changedBy)
        {
            Game = game;
            ChangedBy = changedBy;
        }

        public ChangeReason ChangedBy { get; }

        public IGame Game { get; }
    }
}

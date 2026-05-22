using Empostor.Api.Innersloth;

namespace Empostor.Api.Events
{
    public interface IGameEndedEvent : IGameEvent
    {
        public GameOverReason GameOverReason { get; }
    }
}

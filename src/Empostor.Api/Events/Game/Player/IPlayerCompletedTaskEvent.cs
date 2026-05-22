using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Events.Player
{
    public interface IPlayerCompletedTaskEvent : IPlayerEvent
    {
        ITaskInfo Task { get; }
    }
}

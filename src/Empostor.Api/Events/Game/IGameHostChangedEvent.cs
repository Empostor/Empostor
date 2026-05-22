using Empostor.Api.Net;

namespace Empostor.Api.Events
{
    public interface IGameHostChangedEvent : IGameEvent
    {
        IClientPlayer PreviousHost { get; }

        IClientPlayer? NewHost { get; }
    }
}

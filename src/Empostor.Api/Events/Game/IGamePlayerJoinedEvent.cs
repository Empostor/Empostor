using Empostor.Api.Net;

namespace Empostor.Api.Events
{
    public interface IGamePlayerJoinedEvent : IGameEvent
    {
        IClientPlayer Player { get; }
    }
}

using Empostor.Api.Net;

namespace Empostor.Api.Events
{
    public interface IGamePlayerLeftEvent : IGameEvent
    {
        IClientPlayer Player { get; }

        bool IsBan { get; }
    }
}

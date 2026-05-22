using Empostor.Api.Net;

namespace Empostor.Api.Events.Client
{
    public interface IClientEvent : IEvent
    {
        IHazelConnection Connection { get; }
    }
}

using Empostor.Api.Net;

namespace Empostor.Api.Events.Client
{
    /// <summary>
    ///     Called just after a <see cref="IClient"/> is created and connected.
    /// </summary>
    public interface IClientConnectedEvent : IClientEvent
    {
        IClient Client { get; }
    }
}

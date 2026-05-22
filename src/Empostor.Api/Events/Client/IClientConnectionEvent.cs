using Empostor.Api.Net;

namespace Empostor.Api.Events.Client
{
    /// <summary>
    ///     Called just before a <see cref="IHazelConnection"/> is registered.
    /// </summary>
    public interface IClientConnectionEvent : IClientEvent
    {
        IMessageReader HandshakeData { get; }
    }
}

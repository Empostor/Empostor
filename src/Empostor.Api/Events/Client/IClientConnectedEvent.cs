using Empostor.Api.Net;

namespace Empostor.Api.Events.Client
{
    /// <summary>
    ///     Called after a <see cref="IClient"/> has been fully registered
    ///     and all auth data (including friend code) has been resolved.
    /// </summary>
    public interface IClientConnectedEvent : IClientEvent
    {
        /// <summary>
        ///     Gets the registered <see cref="IClient"/>.
        /// </summary>
        IClient Client { get; }
    }
}

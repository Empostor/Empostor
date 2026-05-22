using Empostor.Api.Games;

namespace Empostor.Api.Events
{
    /// <summary>
    ///     Called whenever a new <see cref="IGame" /> is destroyed.
    /// </summary>
    public interface IGameDestroyedEvent : IGameEvent
    {
    }
}

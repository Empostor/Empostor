using Empostor.Api.Games;

namespace Empostor.Api.Events
{
    public interface IGameEvent : IEvent
    {
        /// <summary>
        ///     Gets the <see cref="IGame" /> this event belongs to.
        /// </summary>
        IGame Game { get; }
    }
}

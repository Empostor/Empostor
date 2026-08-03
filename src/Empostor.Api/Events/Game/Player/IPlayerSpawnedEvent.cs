using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Events.Player
{
    public interface IPlayerSpawnedEvent : IPlayerEvent
    {
        /// <summary>
        ///     Gets the <see cref="IInnerPlayerControl" /> of the spawned player.
        /// </summary>
        IInnerPlayerControl PlayerControl { get; }
    }
}

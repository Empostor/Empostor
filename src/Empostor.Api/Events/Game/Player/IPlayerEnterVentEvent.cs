using Empostor.Api.Innersloth.Maps;

namespace Empostor.Api.Events.Player
{
    /// <summary>
    ///     Called whenever a player enters a vent.
    /// </summary>
    public interface IPlayerEnterVentEvent : IPlayerEvent
    {
        /// <summary>
        ///     Gets the entered vent.
        /// </summary>
        public VentData Vent { get; }
    }
}

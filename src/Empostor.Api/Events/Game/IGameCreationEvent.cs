using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Api.Events
{
    /// <summary>
    ///     Called just before a new <see cref="IGame"/> is created.
    /// </summary>
    public interface IGameCreationEvent : IEventCancelable
    {
        /// <summary>
        ///     Gets the client that requested creation of the game.
        /// </summary>
        /// <remarks>
        ///     Will be null if game creation was requested by a plugin.
        /// </remarks>
        IClient? Client { get; }

        /// <summary>
        ///     Gets or sets the desired <see cref="Games.GameCode"/>.
        /// </summary>
        /// <exception cref="EmpostorException">If the GameCode is invalid or already used in another game.</exception>
        GameCode? GameCode { get; set; }
    }
}

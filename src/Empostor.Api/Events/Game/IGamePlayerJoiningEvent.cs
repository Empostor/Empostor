using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Api.Events
{
    /// <summary>
    ///     Called just before a <see cref="IClientPlayer"/> joins a game.
    /// </summary>
    public interface IGamePlayerJoiningEvent : IGameEvent
    {
        IClientPlayer Player { get; }

        GameJoinResult? JoinResult { get; set; }
    }
}

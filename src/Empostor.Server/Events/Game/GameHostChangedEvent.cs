using Empostor.Api.Events;
using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Server.Events
{
    public class GameHostChangedEvent : IGameHostChangedEvent
    {
        public GameHostChangedEvent(IGame game, IClientPlayer previousHost, IClientPlayer? newHost)
        {
            Game = game;
            PreviousHost = previousHost;
            NewHost = newHost;
        }

        public IGame Game { get; }

        public IClientPlayer PreviousHost { get; }

        public IClientPlayer? NewHost { get; }
    }
}

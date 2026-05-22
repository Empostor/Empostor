using Empostor.Api.Events;
using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Server.Events
{
    public class GameCreatedEvent : IGameCreatedEvent
    {
        public GameCreatedEvent(IGame game, IClient? host)
        {
            Game = game;
            Host = host;
        }

        public IGame Game { get; }

        public IClient? Host { get; }
    }
}

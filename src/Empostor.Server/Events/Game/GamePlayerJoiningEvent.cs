using Empostor.Api.Events;
using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Server.Events
{
    public class GamePlayerJoiningEvent : IGamePlayerJoiningEvent
    {
        public GamePlayerJoiningEvent(IGame game, IClientPlayer player)
        {
            Game = game;
            Player = player;
        }

        public IGame Game { get; }

        public IClientPlayer Player { get; }

        public GameJoinResult? JoinResult { get; set; }
    }
}

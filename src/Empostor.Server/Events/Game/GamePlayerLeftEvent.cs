using Empostor.Api.Events;
using Empostor.Api.Games;
using Empostor.Api.Net;

namespace Empostor.Server.Events
{
    public class GamePlayerLeftEvent : IGamePlayerLeftEvent
    {
        public GamePlayerLeftEvent(IGame game, IClientPlayer player, bool isBan)
        {
            Game = game;
            Player = player;
            IsBan = isBan;
        }

        public IGame Game { get; }

        public IClientPlayer Player { get; }

        public bool IsBan { get; }
    }
}

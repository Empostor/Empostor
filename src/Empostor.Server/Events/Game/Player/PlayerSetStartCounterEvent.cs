using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerSetStartCounterEvent : IPlayerSetStartCounterEvent
    {
        public PlayerSetStartCounterEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, byte secondsLeft)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            SecondsLeft = secondsLeft;
        }

        public byte SecondsLeft { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IGame Game { get; }
    }
}

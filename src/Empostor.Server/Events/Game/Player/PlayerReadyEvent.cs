using Empostor.Api.Events.Game.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Game.Player
{
    public class PlayerReadyEvent : IPlayerReadyEvent
    {
        public PlayerReadyEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }
    }
}

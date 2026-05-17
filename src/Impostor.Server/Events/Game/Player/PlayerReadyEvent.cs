using Impostor.Api.Events.Game.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Server.Events.Game.Player
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

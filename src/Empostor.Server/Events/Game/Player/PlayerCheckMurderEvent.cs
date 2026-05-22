using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerCheckMurderEvent : IPlayerCheckMurderEvent
    {
        public PlayerCheckMurderEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, IInnerPlayerControl victim, MurderResultFlags result)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Victim = victim;
            Result = result;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl Victim { get; }

        public MurderResultFlags Result { get; set; }

        public bool IsCancelled { get; set; }
    }
}

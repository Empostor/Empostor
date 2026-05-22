using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerChatEvent : IPlayerChatEvent
    {
        public PlayerChatEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, string message)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Message = message;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public string Message { get; }

        public bool IsCancelled { get; set; }

        public bool SendToAllPlayers { get; set; } = true;
    }
}

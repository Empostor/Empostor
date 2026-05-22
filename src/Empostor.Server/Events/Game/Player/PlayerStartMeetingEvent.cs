using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerStartMeetingEvent : IPlayerStartMeetingEvent
    {
        public PlayerStartMeetingEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, IInnerPlayerControl? body)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Body = body;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl? Body { get; }
    }
}

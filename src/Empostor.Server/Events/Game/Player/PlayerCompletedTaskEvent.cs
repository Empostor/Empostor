using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerCompletedTaskEvent : IPlayerCompletedTaskEvent
    {
        public PlayerCompletedTaskEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, ITaskInfo task)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Task = task;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public ITaskInfo Task { get; }
    }
}

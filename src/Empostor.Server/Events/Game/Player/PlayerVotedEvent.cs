using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerVotedEvent : IPlayerVotedEvent
    {
        public PlayerVotedEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, VoteType voteType, IInnerPlayerControl? votedFor)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            VoteType = voteType;
            VotedFor = votedFor;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl? VotedFor { get; }

        public VoteType VoteType { get; }
    }
}

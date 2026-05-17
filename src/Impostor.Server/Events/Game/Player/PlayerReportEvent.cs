using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Server.Events.Player
{
    internal sealed class PlayerReportEvent : IPlayerReportEvent
    {
        public PlayerReportEvent(
            IGame game,
            IClientPlayer reporter,
            IInnerPlayerControl reporterControl,
            IClient? reportedClient,
            ReportReasons reason)
        {
            Game = game;
            ClientPlayer = reporter;
            PlayerControl = reporterControl;
            ReportedClient = reportedClient;
            Reason = reason;
            Outcome = ReportOutcome.NotReportedUnknown;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IClient? ReportedClient { get; }

        public ReportReasons Reason { get; }

        public ReportOutcome Outcome { get; set; }
    }
}

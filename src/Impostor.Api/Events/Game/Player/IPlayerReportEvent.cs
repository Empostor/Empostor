using Impostor.Api.Innersloth;
using Impostor.Api.Net;

namespace Impostor.Api.Events.Player
{
    public interface IPlayerReportEvent : IPlayerEvent
    {
        IClient? ReportedClient { get; }

        ReportReasons Reason { get; }

        ReportOutcome Outcome { get; set; }
    }
}

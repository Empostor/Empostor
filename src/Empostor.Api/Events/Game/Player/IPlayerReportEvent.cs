using Empostor.Api.Innersloth;
using Empostor.Api.Net;

namespace Empostor.Api.Events.Player
{
    public interface IPlayerReportEvent : IPlayerEvent
    {
        IClient? ReportedClient { get; }

        ReportReasons Reason { get; }

        ReportOutcome Outcome { get; set; }
    }
}

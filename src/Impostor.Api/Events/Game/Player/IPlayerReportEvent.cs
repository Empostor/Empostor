using Impostor.Api.Innersloth;
using Impostor.Api.Net;

namespace Impostor.Api.Events.Player
{
    /// <summary>
    /// Raised when a player submits an in-game player report.
    /// </summary>
    public interface IPlayerReportEvent : IPlayerEvent
    {
        IClient? ReportedClient { get; }

        ReportReasons Reason { get; }

        ReportOutcome Outcome { get; set; }
    }
}

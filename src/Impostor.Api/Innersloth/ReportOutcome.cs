namespace Impostor.Api.Innersloth
{
    /// <summary>
    /// Outcome returned to the client after a player report.
    /// </summary>
    public enum ReportOutcome : byte
    {
        NotReportedUnknown = 0,
        NotReportedNoAccount = 1,
        NotReportedNotFound = 2,
        NotReportedRateLimit = 3,
        Reported = 4,
    }
}

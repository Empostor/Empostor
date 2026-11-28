namespace Impostor.Api.Innersloth
{
    /// <summary>
    /// Reasons available in the Among Us player-report flow.
    /// </summary>
    public enum ReportReasons : byte
    {
        None = 255,
        InappropriateName = 0,
        InappropriateChat = 1,
        Cheating_Hacking = 2,
        Harassment_Misconduct = 3,
    }
}

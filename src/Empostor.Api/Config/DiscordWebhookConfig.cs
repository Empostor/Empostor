namespace Empostor.Api.Config
{
    public class DiscordWebhookConfig
    {
        public const string Section = "DiscordWebhook";

        public string MatchmakerUrl { get; set; } = string.Empty;

        public string AdminUrl { get; set; } = string.Empty;
        public bool? Enabled { get; set; }
        public string? WebhookUrl { get; set; }
        public bool? NotifyOnGameCreated { get; set; }
        public bool? NotifyOnBan { get; set; }
        public bool? NotifyOnReport { get; set; }
        public bool? NotifyOnPlayerJoin { get; set; }
        public bool? NotifyOnGameEnded { get; set; }
    }
}

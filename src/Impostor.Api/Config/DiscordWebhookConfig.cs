namespace Impostor.Api.Config
{
    public class DiscordWebhookConfig
    {
        public const string Section = "DiscordWebhook";

        public bool Enabled { get; set; } = false;

        public string WebhookUrl { get; set; } = string.Empty;

        public bool NotifyOnGameCreated { get; set; } = true;

        public bool NotifyOnBan { get; set; } = true;

        public bool NotifyOnReport { get; set; } = true;

        public bool NotifyOnPlayerJoin { get; set; } = false;

        public bool NotifyOnGameEnded { get; set; } = false;
    }
}

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookConfig
{
    public string MatchmakerUrl { get; set; } = string.Empty;

    public string AdminUrl { get; set; } = string.Empty;

    /// <summary>Legacy single-webhook fallback used when both URLs are empty.</summary>
    public string WebhookUrl { get; set; } = string.Empty;
}

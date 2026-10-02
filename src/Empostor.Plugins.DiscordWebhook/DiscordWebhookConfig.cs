namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookConfig
{
    public string MatchmakerUrl { get; set; } = string.Empty;

    public string AdminUrl { get; set; } = string.Empty;

    /// <summary>Legacy single-webhook fallback used when both URLs are empty.</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Mirror each lobby with one persistent message that gets edited as the lobby changes,
    ///     instead of posting a separate message per event.
    /// </summary>
    public bool LiveRoomUpdates { get; set; } = true;

    /// <summary>Delete the live message when its lobby is destroyed.</summary>
    public bool DeleteOnClose { get; set; } = true;

    /// <summary>Minimum seconds between two edits of the same lobby message (Discord rate limits).</summary>
    public int MinUpdateIntervalSeconds { get; set; } = 3;
}

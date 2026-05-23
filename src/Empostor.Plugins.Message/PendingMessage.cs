using System;
using System.Text.Json.Serialization;

namespace Empostor.Plugins.Message;

public sealed class PendingMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("sender_name")]
    public string SenderName { get; set; } = "";

    [JsonPropertyName("sender_fc")]
    public string SenderFc { get; set; } = "";

    [JsonPropertyName("target_fc")]
    public string TargetFc { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

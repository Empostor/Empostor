using System.Text.Json.Serialization;

namespace Empostor.Plugins.Message;

public sealed class MessageConfig
{
    [JsonPropertyName("max_messages_per_target")]
    public int MaxMessagesPerTarget { get; set; } = 10;

    [JsonPropertyName("message_max_length")]
    public int MessageMaxLength { get; set; } = 500;
}

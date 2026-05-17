using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Impostor.Plugins.PlayerChannel;

public sealed class PlayerChannelConfig
{
    [JsonPropertyName("channels")]
    public List<ChannelEntry> Channels { get; set; } = new()
    {
        new ChannelEntry
        {
            Name = "Example Channel",
            FriendCodes = new List<string> { "kami#1337", "nofinalsus#8469" },
        },
    };
}

public sealed class ChannelEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("friendCodes")]
    public List<string> FriendCodes { get; set; } = new();
}

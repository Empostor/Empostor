using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Impostor.Plugins.FixedCode;

public sealed class FixedCodeConfig
{
    [JsonPropertyName("mappings")]
    public List<FriendCodeMapping> Mappings { get; set; } = new()
    {
        new FriendCodeMapping
        {
            FriendCode = "kami#1337",
            RoomCode = "DUCK",
        },
    };
}

public sealed class FriendCodeMapping
{
    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("roomCode")]
    public string RoomCode { get; set; } = string.Empty;
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Impostor.Plugins.Titles;

public sealed class TitlesConfig
{
    [JsonPropertyName("titles")]
    public List<FriendCodeTitle> Titles { get; set; } = new()
    {
        new FriendCodeTitle { FriendCode = "aideproof#8388", Title = "Empostor" },
    };
}

public sealed class FriendCodeTitle
{
    [JsonPropertyName("friendCode")]
    public string FriendCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

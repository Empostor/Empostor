using System.Text.Json.Serialization;

namespace Empostor.Plugins.MapVote;

public sealed class MapVoteConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("require_majority")]
    public bool RequireMajority { get; set; } = false;

    [JsonPropertyName("allow_host_override")]
    public bool AllowHostOverride { get; set; } = true;
}

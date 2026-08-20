using System;
using System.Text.Json.Serialization;

namespace Empostor.Api.Innersloth.GameFilters
{
    [Serializable]
    public class MapGameFilter : ISubFilter
    {
        [JsonPropertyName("FilterType")]
        public string FilterType { get; } = "map";

        [JsonPropertyName("AcceptedValues")]
        public required byte AcceptedValues { get; set; }
    }
}

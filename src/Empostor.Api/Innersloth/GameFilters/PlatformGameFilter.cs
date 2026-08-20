using System;
using System.Text.Json.Serialization;

namespace Empostor.Api.Innersloth.GameFilters
{
    [Serializable]
    public class PlatformGameFilter : ISubFilter
    {
        [JsonPropertyName("FilterType")]
        public string FilterType { get; } = "platform";

        [JsonPropertyName("AcceptedValues")]
        public required uint AcceptedValues { get; set; }
    }
}

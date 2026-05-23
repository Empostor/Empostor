using System.Text.Json.Serialization;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorConfig
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "deepseek-v4-flash";

    [JsonPropertyName("apiEndpoint")]
    public string ApiEndpoint { get; set; } = "https://api.deepseek.com/";
}

using System.Text.Json.Serialization;

namespace Impostor.Plugins.QqVerify;

public sealed class QqVerifyConfig
{
    [JsonPropertyName("botSecret")]
    public string BotSecret { get; set; } = "change-bot-secret";
}

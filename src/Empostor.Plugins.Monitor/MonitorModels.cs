using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Empostor.Plugins.Monitor;

public sealed class MonitorStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("uptime_seconds")]
    public long UptimeSeconds { get; set; }

    [JsonPropertyName("uptime_display")]
    public string UptimeDisplay { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("processors")]
    public int Processors { get; set; }

    [JsonPropertyName("memory_mb")]
    public long MemoryMb { get; set; }

    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("player_count")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("active_connections")]
    public int ActiveConnections { get; set; }

    [JsonPropertyName("games")]
    public List<MonitorGame> Games { get; set; } = new();
}

public sealed class MonitorGame
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("player_count")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;
}

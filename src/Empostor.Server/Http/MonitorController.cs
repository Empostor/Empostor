using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Empostor.Api.Games.Managers;
using Empostor.Api.Net.Manager;
using Microsoft.AspNetCore.Mvc;

namespace Empostor.Server.Http;

[ApiController]
public sealed class MonitorController : ControllerBase
{
    private static readonly DateTime StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly IGameManager _gameManager;
    private readonly IClientManager _clientManager;

    public MonitorController(IGameManager gameManager, IClientManager clientManager)
    {
        _gameManager = gameManager;
        _clientManager = clientManager;
    }

    [HttpGet("/api/monitor/status")]
    public IActionResult GetStatus()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - StartTime;

        var games = _gameManager.Games.ToList();
        var clients = _clientManager.Clients.ToList();

        var status = new MonitorStatus
        {
            Status = "running",
            Timestamp = DateTime.UtcNow.ToString("o"),
            UptimeSeconds = (long)uptime.TotalSeconds,
            UptimeDisplay = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m",
            Version = GetVersion(),
            Runtime = RuntimeInformation.FrameworkDescription,
            Platform = RuntimeInformation.OSDescription,
            Processors = Environment.ProcessorCount,
            MemoryMb = process.WorkingSet64 / 1024 / 1024,
            GameCount = games.Count,
            PlayerCount = clients.Count(c => c.Player != null),
            ActiveConnections = clients.Count,
            Games = games.Select(g => new MonitorGame
            {
                Code = g.Code.ToString(),
                State = g.GameState.ToString(),
                Map = g.Options.Map.ToString(),
                PlayerCount = g.PlayerCount,
                Host = g.Host?.Character?.PlayerInfo?.PlayerName ?? "(none)",
            }).ToList(),
        };

        return new JsonResult(status);
    }

    [HttpGet("/api/monitor/health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("o") });
    }

    private static string GetVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return info.ProductVersion ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

public sealed class MonitorStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("uptime_seconds")]
    public long UptimeSeconds { get; set; }

    [JsonPropertyName("uptime_display")]
    public string UptimeDisplay { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

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
    public string Code { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("player_count")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";
}

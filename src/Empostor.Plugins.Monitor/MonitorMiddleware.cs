using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Games.Managers;
using Empostor.Api.Net.Manager;
using Microsoft.AspNetCore.Http;

namespace Empostor.Plugins.Monitor;

public sealed class MonitorMiddleware
{
    private static readonly DateTime StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly RequestDelegate _next;

    public MonitorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGameManager gameManager, IClientManager clientManager)
    {
        var path = context.Request.Path;

        if (path.Equals("/api/monitor/status", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            await WriteJsonAsync(context, BuildStatus(gameManager, clientManager));
            return;
        }

        if (path.Equals("/api/monitor/health", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            await WriteJsonAsync(context, new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("o"),
            });
            return;
        }

        await _next(context);
    }

    private static MonitorStatus BuildStatus(IGameManager gameManager, IClientManager clientManager)
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - StartTime;
        var games = gameManager.Games.ToList();
        var clients = clientManager.Clients.ToList();

        return new MonitorStatus
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
    }

    private static async Task WriteJsonAsync(HttpContext context, object value)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static string GetVersion()
    {
        try
        {
            var assembly = typeof(MonitorMiddleware).Assembly;
            var info = FileVersionInfo.GetVersionInfo(assembly.Location);
            return info.ProductVersion ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

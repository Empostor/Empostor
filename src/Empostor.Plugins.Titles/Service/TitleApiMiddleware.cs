using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Titles.Service;

public sealed class TitleApiMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TitleApiMiddleware> _logger;
    private readonly TitlesConfig _config;
    private readonly string _configPath;
    private readonly FriendCodeTitleListener? _friendCodeListener;

    public TitleApiMiddleware(
        RequestDelegate next,
        ILogger<TitleApiMiddleware> logger,
        TitlesConfig config,
        string configPath,
        FriendCodeTitleListener? friendCodeListener = null)
    {
        _next = next;
        _logger = logger;
        _config = config;
        _configPath = configPath;
        _friendCodeListener = friendCodeListener;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/api/title/add"
            && context.Request.Method == "POST")
        {
            await HandleAddTitleAsync(context);
            return;
        }

        await _next(context);
    }

    private async Task HandleAddTitleAsync(HttpContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            var request = JsonSerializer.Deserialize<TitleAddRequest>(body);
            if (request == null
                || string.IsNullOrWhiteSpace(request.FriendCode)
                || string.IsNullOrWhiteSpace(request.Title))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"ok\":false,\"error\":\"friendCode and title are required.\"}");
                return;
            }

            // Add to config
            _config.Titles.Add(new FriendCodeTitle
            {
                FriendCode = request.FriendCode.Trim(),
                Title = request.Title.Trim()
            });

            // Persist to file
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
            await File.WriteAllTextAsync(_configPath, json);

            // Reload mappings in the listener
            _friendCodeListener?.Reload();

            var addedBy = request.AddedBy ?? "unknown";
            _logger.LogInformation(
                "[TitleApi] Added title [{Title}] for friend code {FC} (added by {AddedBy})",
                request.Title, request.FriendCode, addedBy);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"ok\":true}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TitleApi] Failed to add title");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"ok\":false,\"error\":\"Internal server error.\"}");
        }
    }

    private sealed record TitleAddRequest(
        [property: JsonPropertyName("friendCode")]
        string? FriendCode,
        [property: JsonPropertyName("title")]
        string? Title,
        [property: JsonPropertyName("addedBy")]
        string? AddedBy);
}

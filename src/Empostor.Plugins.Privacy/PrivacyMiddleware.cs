using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Empostor.Plugins.Privacy;

public sealed class PrivacyMiddleware
{
    private readonly RequestDelegate _next;

    public PrivacyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PrivacyStore store)
    {
        var path = context.Request.Path;

        if (path.Equals("/privacy", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(store.GetContent());
            return;
        }

        if (path.Equals("/admin/api/privacy", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            await HandleUpdateAsync(context, store);
            return;
        }

        await _next(context);
    }

    private static async Task HandleUpdateAsync(HttpContext context, PrivacyStore store)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        try
        {
            var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("content").GetString() ?? string.Empty;
            var token = doc.RootElement.GetProperty("token").GetString() ?? string.Empty;

            var adminToken = Environment.GetEnvironmentVariable("EMP_HTTP_TOKEN")
                          ?? Environment.GetEnvironmentVariable("EMP_ADMIN_TOKEN")
                          ?? "empostor";

            if (token != adminToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Invalid token.\"}");
                return;
            }

            store.SaveContent(content);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":true}");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            await context.Response.WriteAsync(error);
        }
    }
}

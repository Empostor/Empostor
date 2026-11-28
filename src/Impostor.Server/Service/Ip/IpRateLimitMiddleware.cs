using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Ip;

public sealed class IpRateLimitMiddleware
{
    // The client parses a failed matchmaker request as a MatchmakerResponse
    // and reads "Errors"; 116 (TooManyRequests) is rendered by the client itself.
    private static readonly JsonSerializerOptions MatchmakerJsonOptions = new();

    private readonly RequestDelegate _next;
    private readonly IpRateLimitService _rateLimit;
    private readonly ILogger<IpRateLimitMiddleware> _logger;

    public IpRateLimitMiddleware(
        RequestDelegate next,
        IpRateLimitService rateLimit,
        ILogger<IpRateLimitMiddleware> logger)
    {
        _next = next;
        _rateLimit = rateLimit;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = RealIpResolver.Resolve(context);

        // Every request is counted and rejected over quota, the join
        // flow included: no token or port is handed out, so no pool port is spent.
        if (_rateLimit.TryRegister(ip, out var retryAfter))
        {
            await _next(context);
            return;
        }

        var minutes = IpRateLimitService.MinutesUntilRetry(retryAfter);

        _logger.LogWarning(
            "IpRateLimit blocked {Ip} on {Path} │ retry in {Minutes}m",
            ip?.ToString() ?? "unknown",
            context.Request.Path.Value ?? "/",
            minutes);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new { Errors = new[] { new { Reason = DisconnectReason.TooManyRequests } } },
            MatchmakerJsonOptions));
    }
}

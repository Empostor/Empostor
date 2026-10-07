using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Server.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Ip;

public sealed class IpRateLimitMiddleware
{
    private const string MessageKey = IpRateLimitService.MessageKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly RequestDelegate _next;
    private readonly IpRateLimitService _rateLimit;
    private readonly ILocalizationService _language;
    private readonly ILogger<IpRateLimitMiddleware> _logger;

    public IpRateLimitMiddleware(
        RequestDelegate next,
        IpRateLimitService rateLimit,
        ILocalizationService language,
        ILogger<IpRateLimitMiddleware> logger)
    {
        _next = next;
        _rateLimit = rateLimit;
        _language = language;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // The admin panel polls its own endpoints, so it neither consumes the
        // quota nor is ever rate-limited.
        if (IsAdminPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var ip = RealIpResolver.Resolve(context);

        // Always counted, so the join endpoint cannot be used to burn through
        // the quota without being noticed.
        var overQuota = !_rateLimit.TryRegister(ip, out var retryAfter);

        // The join flow of the game client (POST /api/user, then
        // PUT/GET /api/games for FindHost) must never fail over HTTP: the
        // client only logs such a failure and then hangs on a black screen.
        // These endpoints are therefore counted but never blocked — over-quota
        // players still take no pool port and no outbound auth call, and are
        // pointed at one shared reject port whose listener sends the localized
        // hint in-game (see TokenController / Matchmaker).
        if (!overQuota || IsGameFlowPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var minutes = IpRateLimitService.MinutesUntilRetry(retryAfter);

        var message = _language.Get(new LocalizedMessageKey("empostor", MessageKey), LanguageCodes.FromTag(ResolveLangCode(context)), minutes);

        _logger.LogWarning(
            "IpRateLimit blocked {Ip} on {Path} │ retry in {Minutes}m",
            ip?.ToString() ?? "unknown",
            context.Request.Path.Value ?? "/",
            minutes);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }, JsonOptions));
    }

    // The admin panel polls its own endpoints, so it is never rate-limited.
    private static bool IsAdminPath(PathString path)
        => path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);

    // The join flow of the game client: matchmaking token + FindHost.
    // GamesController echoes the token's port back, so the reject port handed
    // out by /api/user is what the client connects to.
    private static bool IsGameFlowPath(PathString path)
        => path.StartsWithSegments("/api/user", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/api/games", StringComparison.OrdinalIgnoreCase);

    private static string ResolveLangCode(HttpContext context)
    {
        var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();
        foreach (var entry in acceptLanguage.Split(','))
        {
            var tag = entry.Split(';')[0].Trim();
            if (tag.Length == 0 || tag == "*")
            {
                continue;
            }

            var code = MapLangTag(tag);
            if (code != null)
            {
                return code;
            }
        }

        return "en";
    }

    // Maps a browser/client language tag (zh-CN, pt-BR, ...) to one of the
    // server's Languages/*.json codes.
    private static string? MapLangTag(string tag)
    {
        var lower = tag.ToLowerInvariant();

        if (lower.StartsWith("zh", StringComparison.Ordinal))
        {
            return lower.Contains("hant", StringComparison.Ordinal)
                   || lower.Contains("tw", StringComparison.Ordinal)
                   || lower.Contains("hk", StringComparison.Ordinal)
                   || lower.Contains("mo", StringComparison.Ordinal)
                ? "zh_TW"
                : "zh_CN";
        }

        if (lower.StartsWith("pt-br", StringComparison.Ordinal))
        {
            return "pt_BR";
        }

        if (lower.StartsWith("fil", StringComparison.Ordinal) || lower.StartsWith("tl", StringComparison.Ordinal))
        {
            return "fil";
        }

        foreach (var code in new[] { "en", "es", "pt", "ko", "ru", "nl", "fr", "de", "it", "ja", "ga" })
        {
            if (lower.StartsWith(code, StringComparison.Ordinal))
            {
                return code;
            }
        }

        return null;
    }
}

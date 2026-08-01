using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Service;
using Empostor.Api.Events.Game.Player;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Welcome.Service;

public sealed class WelcomeEventListener : IEventListener
{
    private const string TextDir = "Message";
    private const string FallbackFile = "EnglishHelloWord.txt";

    private static readonly Regex CaveRegex = new(
        @"\<cave\>(.+?)\</cave\>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex RandomRegex = new(
        @"\<random\s*=\s*(\[[^\]]*\])\s*>(.*?)</random\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline,
        TimeSpan.FromMilliseconds(100));

    private static readonly Random Rng = new();

    private readonly ILogger<WelcomeEventListener> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WelcomeEventListener(
        ILogger<WelcomeEventListener> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [EventListener]
    public void OnPlayerSpawned(IPlayerReadyEvent e)
    {
        if (e.Game.GameState != GameStates.NotStarted) return;

        var player = e.ClientPlayer;
        var playerCtrl = e.PlayerControl;

        Task.Run(async () =>
        {
            try
            {
                if (player.Client.Connection == null || !player.Client.Connection.IsConnected)
                    return;

                var baseDir = Path.Combine(Directory.GetCurrentDirectory(), TextDir);
                var filePath = Path.Combine(baseDir, $"{player.Client.Language}HelloWord.txt");

                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(baseDir, FallbackFile);
                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("[Welcome] No HelloWord.txt found for language {Language}, fallback missing too.", player.Client.Language);
                        return;
                    }
                }

                var template = File.ReadAllText(filePath);

                // Process <cave>URL</cave> tags before final formatting
                template = await ProcessCaveTagsAsync(template);

                // Process <random = [...]>default</random> tags
                template = ProcessRandomTags(template);

                var message = FormatMessage(template, player);

                await playerCtrl.SendChatToPlayerAsync(message, playerCtrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Welcome] Failed to send welcome message");
            }
        });
    }

    /// <summary>
    ///     Finds all <cave>URL</cave> tags in the template, fetches content from each URL,
    ///     and replaces the tags with the fetched text.
    /// </summary>
    private async Task<string> ProcessCaveTagsAsync(string template)
    {
        var matches = CaveRegex.Matches(template);
        if (matches.Count == 0) return template;

        foreach (Match match in matches)
        {
            var url = match.Groups[1].Value.Trim();
            var replacement = await FetchCaveContentAsync(url);
            template = template.Replace(match.Value, replacement);
        }

        return template;
    }

    /// <summary>
    ///     Finds all <random = ["a","b","c"]>default</random> tags in the template,
    ///     randomly picks one option from the JSON array, and replaces the tag.
    ///     Falls back to the default text between the tags on any error.
    /// </summary>
    private static string ProcessRandomTags(string template)
    {
        var matches = RandomRegex.Matches(template);
        if (matches.Count == 0) return template;

        foreach (Match match in matches)
        {
            var jsonArray = match.Groups[1].Value.Trim();
            var defaultValue = match.Groups[2].Value;
            var replacement = PickRandomOption(jsonArray, defaultValue);
            template = template.Replace(match.Value, replacement);
        }

        return template;
    }

    /// <summary>
    ///     Parses a JSON string array like ["a","b","c"] and picks one element at random.
    ///     Returns <paramref name="defaultValue"/> on any parse error or empty array.
    /// </summary>
    private static string PickRandomOption(string jsonArray, string defaultValue)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonArray);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return defaultValue;

            var items = new System.Collections.Generic.List<string>();
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    items.Add(item.GetString()!);
                }
                else
                {
                    items.Add(item.ToString());
                }
            }

            if (items.Count == 0) return defaultValue;

            return items[Rng.Next(items.Count)];
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    ///     Fetches content from a URL. Supports:
    ///     - Plain text responses (returned as-is)
    ///     - JSON responses with a "hitokoto" field (Hitokoto API format)
    ///     - JSON responses with a "content" field
    ///     Returns an empty string on failure.
    /// </summary>
    private async Task<string> FetchCaveContentAsync(string url)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("cave");
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Welcome] <cave> fetch failed for {Url}: HTTP {Status}",
                    url, (int)response.StatusCode);
                return string.Empty;
            }

            var body = await response.Content.ReadAsStringAsync();

            // Try to detect content type
            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (contentType != null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseJsonContent(body);
            }

            // If content-type is not JSON, try to detect JSON anyway
            if (body.TrimStart().StartsWith("{"))
            {
                return TryParseJsonContent(body);
            }

            // Plain text — return directly
            return body.Trim();
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("[Welcome] <cave> fetch timed out for {Url}", url);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Welcome] <cave> fetch error for {Url}", url);
            return string.Empty;
        }
    }

    /// <summary>
    ///     Tries to parse JSON content looking for known fields.
    ///     Falls back to returning the raw trimmed string.
    /// </summary>
    private static string TryParseJsonContent(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Hitokoto API: {"hitokoto": "...", "from": "..."}
            if (root.TryGetProperty("hitokoto", out var hitokoto))
            {
                var text = hitokoto.GetString() ?? string.Empty;
                if (root.TryGetProperty("from", out var from) && !string.IsNullOrWhiteSpace(from.GetString()))
                {
                    text += $" —— {from.GetString()}";
                }

                return text;
            }

            // Generic: {"content": "..."}
            if (root.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            // Unknown JSON — return raw
            return body.Trim();
        }
        catch
        {
            return body.Trim();
        }
    }

    private static string FormatMessage(string template, Api.Net.IClientPlayer player)
    {
        var lastConnect = player.Client.ProductUserId != null
            ? PlayerConnectStore.GetLastConnectString(player.Client.ProductUserId)
            : null;

        return template
            .Replace("{Name}", player.Client.Name ?? "Player")
            .Replace("{Room}", player.Game.Code.Code)
            .Replace("{FriendCode}", player.Client.FriendCode ?? "None")
            .Replace("{LastConnect}", lastConnect ?? "First time!");
    }
}

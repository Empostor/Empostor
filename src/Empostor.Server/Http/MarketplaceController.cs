using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Server.Http.Admin;
using Empostor.Server.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http
{
    [ApiController]
    public sealed class MarketplaceController : ControllerBase
    {
        private static readonly string PluginsDir =
            Path.Combine(Directory.GetCurrentDirectory(), "plugins");

        private static readonly Regex ThemeIdPattern = new("^[a-z0-9][a-z0-9-]{1,40}$", RegexOptions.Compiled);

        private readonly ILogger<MarketplaceController> _logger;
        private readonly IHttpClientFactory _http;
        private readonly AdminConfig _config;
        private readonly PluginLoaderService _pluginLoaderService;
        private readonly AdminThemeRegistry _themes;
        private readonly string _passwordHash;

        public MarketplaceController(
            ILogger<MarketplaceController> logger,
            IHttpClientFactory http,
            IOptions<AdminConfig> config,
            PluginLoaderService pluginLoaderService,
            AdminThemeRegistry themes)
        {
            _logger = logger;
            _http = http;
            _config = config.Value;
            _pluginLoaderService = pluginLoaderService;
            _themes = themes;
            _passwordHash = AdminController.ComputeHash(_config.Password);
        }

        private bool IsAuthenticated() => AdminSession.IsAuthenticated(HttpContext, _passwordHash);

        [HttpGet("/api/admin/marketplace/plugins")]
        public async Task<IActionResult> ListPlugins()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var url = _config.MarketplaceUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest(new { error = "MarketplaceUrl is not configured." });
            }

            try
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(url);

                var installedIds = new HashSet<string>(
                    _pluginLoaderService.Plugins.Select(p => p.Id),
                    StringComparer.OrdinalIgnoreCase);

                using var doc = JsonDocument.Parse(json);
                var plugins = new List<JsonElement>();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var id = element.GetProperty("id").GetString() ?? string.Empty;
                    var installed = installedIds.Contains(id);

                    var obj = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Name == "installed")
                        {
                            continue;
                        }

                        obj[prop.Name] = JsonValueToObject(prop.Value);
                    }

                    obj["installed"] = installed;
                    plugins.Add(JsonSerializer.SerializeToElement(obj));
                }

                var result = JsonSerializer.Serialize(plugins);
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MarketplaceFailed to fetch {Url}", url);
                return StatusCode(502, new { error = "Failed to fetch marketplace: " + ex.Message });
            }
        }

        [HttpPost("/api/admin/marketplace/install")]
        public async Task<IActionResult> InstallPlugin([FromBody] InstallRequest req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.DownloadUrl))
            {
                return BadRequest(new { error = "DownloadUrl is required" });
            }

            if (!req.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only HTTPS download URLs are allowed" });
            }

            if (!string.IsNullOrEmpty(req.PluginId))
            {
                var installedIds = new HashSet<string>(
                    _pluginLoaderService.Plugins.Select(p => p.Id),
                    StringComparer.OrdinalIgnoreCase);

                if (installedIds.Contains(req.PluginId))
                {
                    return BadRequest(new { error = "This plugin is already installed." });
                }
            }

            try
            {
                Directory.CreateDirectory(PluginsDir);

                using var client = CreateClient();
                client.Timeout = TimeSpan.FromSeconds(60);

                var fileName = Path.GetFileName(new Uri(req.DownloadUrl).LocalPath);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".dll";
                }

                var bytes = await client.GetByteArrayAsync(req.DownloadUrl);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(PluginsDir, fileName), bytes);

                _logger.LogInformation("MarketplaceInstalled {File} from {Url}", fileName, req.DownloadUrl);
                return Ok(new { installed = fileName, restartRequired = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MarketplaceInstall failed: {Url}", req.DownloadUrl);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("/api/admin/marketplace/themes")]
        public async Task<IActionResult> ListThemes()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            _themes.Rescan();

            var remote = new List<RemoteTheme>();
            var url = _config.ThemeMarketplaceUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    using var client = CreateClient();
                    var json = await client.GetStringAsync(url);
                    remote = JsonSerializer.Deserialize<List<RemoteTheme>>(
                                 json,
                                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                             ?? new List<RemoteTheme>();
                }
                catch (Exception ex)
                {
                    // A missing or unreachable catalogue must not hide the local themes.
                    _logger.LogWarning(ex, "MarketplaceFailed to fetch themes from {Url}", url);
                }
            }

            var themes = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _themes.Themes)
            {
                if (!seen.Add(entry.Id))
                {
                    continue;
                }

                var info = remote.FirstOrDefault(r => string.Equals(r.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                themes.Add(new
                {
                    id = entry.Id,
                    name = entry.Name,
                    source = entry.Source,
                    installed = true,
                    description = string.IsNullOrWhiteSpace(info?.Description) ? DefaultDescription(entry.Source) : info!.Description,
                    author = info?.Author,
                    downloadUrl = info?.DownloadUrl,
                    tokens = _themes.Resolve(entry.Id)?.Tokens,
                    darkTokens = _themes.Resolve(entry.Id)?.DarkTokens,
                });
            }

            foreach (var entry in remote)
            {
                if (string.IsNullOrWhiteSpace(entry.Id) || !seen.Add(entry.Id))
                {
                    continue;
                }

                themes.Add(new
                {
                    id = entry.Id,
                    name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
                    source = "remote",
                    installed = false,
                    description = entry.Description,
                    author = entry.Author,
                    downloadUrl = entry.DownloadUrl,
                    tokens = (Dictionary<string, string>?)null,
                    darkTokens = (Dictionary<string, string>?)null,
                });
            }

            return Ok(themes);
        }

        /// <summary>
        ///     Writes <c>Pages/themes/{Id}/theme.json</c>. The registry is rescanned afterwards, so the
        ///     theme is switchable immediately without restarting the server.
        /// </summary>
        [HttpPost("/api/admin/marketplace/themes/install")]
        public async Task<IActionResult> InstallTheme([FromBody] ThemeInstallRequest req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var id = (req.Id ?? string.Empty).Trim().ToLowerInvariant();
            if (!ThemeIdPattern.IsMatch(id))
            {
                return BadRequest(new { error = "A theme id of lowercase letters, digits and dashes is required." });
            }

            if (string.IsNullOrWhiteSpace(req.DownloadUrl)
                || !req.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only HTTPS download URLs are allowed" });
            }

            try
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(req.DownloadUrl);

                // Validate before touching disk: it must parse and declare the id we are installing.
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest(new { error = "The downloaded file is not a theme.json object." });
                }

                var declared = ReadId(doc.RootElement);
                if (!string.Equals(declared, id, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = $"theme.json declares id '{declared}', expected '{id}'." });
                }

                var directory = Path.Combine(AdminThemeRegistry.ThemesDirectory, id);
                Directory.CreateDirectory(directory);
                await System.IO.File.WriteAllTextAsync(Path.Combine(directory, "theme.json"), json);

                _themes.Rescan();
                _logger.LogInformation("MarketplaceInstalled theme {Id} from {Url}", id, req.DownloadUrl);
                return Ok(new { installed = id, restartRequired = false });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MarketplaceInstall theme failed: {Url}", req.DownloadUrl);
                return StatusCode(502, new { error = ex.Message });
            }
        }

        private static string DefaultDescription(string source) => source switch
        {
            "builtin" => "The stock look of the admin panel.",
            "plugin" => "Shipped by an installed plugin.",
            _ => "Installed on this server.",
        };

        private static string? ReadId(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var name in new[] { "Id", "id" })
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }

        private HttpClient CreateClient()
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Empostor-Marketplace/1.0");
            return client;
        }

        private static object? JsonValueToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText()),
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(element.GetRawText()),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => null,
            };
        }

        public sealed record InstallRequest(string DownloadUrl, string? PluginId = null);

        public sealed record ThemeInstallRequest(string Id, string DownloadUrl);

        private sealed class RemoteTheme
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public string? Description { get; set; }

            public string? Author { get; set; }

            public string? DownloadUrl { get; set; }
        }
    }
}

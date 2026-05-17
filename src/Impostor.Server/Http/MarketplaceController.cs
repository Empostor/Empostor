using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Http
{
    [ApiController]
    public sealed class MarketplaceController : ControllerBase
    {
        private static readonly string PluginsDir =
            Path.Combine(Directory.GetCurrentDirectory(), "plugins");

        private const string EmpostorRepo = "Empostor/Empostor";

        private readonly ILogger<MarketplaceController> _logger;
        private readonly IHttpClientFactory _http;
        private readonly AdminConfig _config;

        public MarketplaceController(
            ILogger<MarketplaceController> logger,
            IHttpClientFactory http,
            IOptions<AdminConfig> config)
        {
            _logger = logger;
            _http = http;
            _config = config.Value;
        }

        private bool IsAuthenticated()
            => Request.Cookies.TryGetValue("impostor_admin", out var v) && v == _config.Password;

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
                using var client = _http.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Empostor-Marketplace/1.0");
                var json = await client.GetStringAsync(url);
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Marketplace] Failed to fetch {Url}", url);
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

            try
            {
                Directory.CreateDirectory(PluginsDir);

                using var client = _http.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Empostor-Marketplace/1.0");
                client.Timeout = TimeSpan.FromSeconds(60);

                var fileName = Path.GetFileName(new Uri(req.DownloadUrl).LocalPath);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".dll";
                }

                var bytes = await client.GetByteArrayAsync(req.DownloadUrl);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(PluginsDir, fileName), bytes);

                _logger.LogInformation("[Marketplace] Installed {File} from {Url}", fileName, req.DownloadUrl);
                return Ok(new { installed = fileName, restartRequired = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Marketplace] Install failed: {Url}", req.DownloadUrl);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("/api/admin/update/check")]
        public async Task<IActionResult> CheckUpdate()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var current = Utils.DotnetUtils.Version;
            try
            {
                using var client = _http.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Empostor-UpdateCheck/1.0");
                var json = await client.GetStringAsync(
                    $"https://api.github.com/repos/{EmpostorRepo}/releases/latest");
                using var doc = JsonDocument.Parse(json);
                var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
                var url = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;
                var name = doc.RootElement.GetProperty("name").GetString() ?? tag;
                var latest = tag.TrimStart('v');
                var isCurrent = string.Equals(
                    current.Split('+')[0], latest, StringComparison.OrdinalIgnoreCase);
                return Ok(new { currentVersion = current, latestVersion = latest, latestTag = tag, latestName = name, releaseUrl = url, upToDate = isCurrent });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Update] GitHub check failed");
                return StatusCode(502, new { error = ex.Message });
            }
        }

        public sealed record InstallRequest(string DownloadUrl);
    }
}

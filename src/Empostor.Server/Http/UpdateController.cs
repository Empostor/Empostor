using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Server.Http.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http
{
    /// <summary>
    ///     Server-version endpoints for the admin panel's Updates tab: querying GitHub releases
    ///     (stable / nightly / an explicit tag), listing them for manual selection, and downloading
    ///     the package that matches this machine. Updating the running installation stays manual.
    /// </summary>
    [ApiController]
    public sealed class UpdateController : ControllerBase
    {
        private const string EmpostorRepo = "Empostor/Empostor";

        private const string ChannelStable = "stable";
        private const string ChannelNightly = "nightly";
        private const string ChannelTag = "tag";

        /// <summary>Packages downloaded from the update panel land in <c>Update/{Version}/</c>.</summary>
        private const string UpdateRootDirName = "Update";

        private static readonly string[] SupportedRids = { "win-x64", "linux-x64", "linux-arm", "linux-arm64", "osx-x64" };

        private static readonly Regex TagNamePattern = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);

        private static readonly Regex VersionInName = new(@"\(([^)]+)\)", RegexOptions.Compiled);

        /// <summary>
        ///     Successful lookups are cached for a minute so repeated clicks cannot burn the API quota;
        ///     GitHub only allows 60 anonymous requests per hour per IP.
        /// </summary>
        private static readonly ConcurrentDictionary<string, (DateTimeOffset Expiry, string Json)> ResponseCache = new();

        private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

        private readonly ILogger<UpdateController> _logger;
        private readonly IHttpClientFactory _http;
        private readonly string _passwordHash;
        private readonly string _gitHubToken;

        public UpdateController(
            ILogger<UpdateController> logger,
            IHttpClientFactory http,
            IOptions<AdminConfig> config)
        {
            _logger = logger;
            _http = http;
            _passwordHash = AdminController.ComputeHash(config.Value.Password);
            _gitHubToken = (config.Value.GitHubToken ?? string.Empty).Trim();
        }

        private bool IsAuthenticated() => AdminSession.IsAuthenticated(HttpContext, _passwordHash);

        [HttpGet("/api/admin/update/check")]
        public async Task<IActionResult> CheckUpdate([FromQuery] string? channel = null, [FromQuery] string? tag = null)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var (mode, target, channelError) = ResolveChannel(channel, tag);
            if (channelError != null)
            {
                return BadRequest(new { error = channelError });
            }

            var current = Utils.DotnetUtils.Version;
            try
            {
                using var client = CreateClient();
                var fetch = await FetchAsync(client, ReleaseApiUrl(mode, target));
                if (!fetch.Ok)
                {
                    return GitHubError(fetch);
                }

                using var doc = JsonDocument.Parse(fetch.Json!);
                var root = doc.RootElement;
                var tagName = GetString(root, "tag_name");
                var name = GetString(root, "name");
                var url = GetString(root, "html_url");
                var latest = mode == ChannelStable ? tagName.TrimStart('v') : ExtractVersion(name, tagName);
                var isCurrent = string.Equals(
                    current.Split('+')[0], latest, StringComparison.OrdinalIgnoreCase);
                var rid = CurrentPlatformRid();
                var package = FindPlatformAsset(root, rid);
                return Ok(new
                {
                    channel = mode,
                    queryTag = mode == ChannelTag ? target : tagName,
                    currentVersion = current,
                    latestVersion = latest,
                    latestTag = tagName,
                    latestName = string.IsNullOrWhiteSpace(name) ? tagName : name,
                    releaseUrl = url,
                    isPrerelease = IsTrue(root, "prerelease"),
                    publishedAt = GetString(root, "published_at"),
                    upToDate = isCurrent,
                    platform = rid,
                    platformAsset = package?.Name,
                    platformAssetSize = package?.Size ?? 0,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateGitHub check failed for channel {Channel}", mode);
                return StatusCode(502, new { error = ex.Message });
            }
        }

        [HttpGet("/api/admin/update/releases")]
        public async Task<IActionResult> ListReleases([FromQuery] int limit = 30)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            limit = Math.Clamp(limit, 1, 100);
            try
            {
                using var client = CreateClient();
                var fetch = await FetchAsync(
                    client, $"https://api.github.com/repos/{EmpostorRepo}/releases?per_page={limit}");
                if (!fetch.Ok)
                {
                    return GitHubError(fetch);
                }

                using var doc = JsonDocument.Parse(fetch.Json!);

                var releases = new List<object>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (IsTrue(element, "draft"))
                    {
                        continue;
                    }

                    var tagName = GetString(element, "tag_name");
                    var name = GetString(element, "name");
                    releases.Add(new
                    {
                        tag = tagName,
                        name = string.IsNullOrWhiteSpace(name) ? tagName : name,
                        version = ExtractVersion(name, tagName),
                        prerelease = IsTrue(element, "prerelease"),
                        publishedAt = GetString(element, "published_at"),
                        releaseUrl = GetString(element, "html_url"),
                    });
                }

                return Ok(releases);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateGitHub release list failed");
                return StatusCode(502, new { error = ex.Message });
            }
        }

        /// <summary>
        ///     Downloads the package matching this machine into <c>Update/{version}/{original asset name}</c>.
        /// </summary>
        [HttpPost("/api/admin/update/download")]
        public async Task<IActionResult> DownloadUpdate([FromBody] UpdateDownloadRequest req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var (mode, target, channelError) = ResolveChannel(req.Channel, req.Tag);
            if (channelError != null)
            {
                return BadRequest(new { error = channelError });
            }

            try
            {
                using var client = CreateClient();
                var fetch = await FetchAsync(client, ReleaseApiUrl(mode, target));
                if (!fetch.Ok)
                {
                    return GitHubError(fetch);
                }

                using var doc = JsonDocument.Parse(fetch.Json!);
                var root = doc.RootElement;
                var tagName = GetString(root, "tag_name");
                var version = mode == ChannelStable
                    ? tagName.TrimStart('v')
                    : ExtractVersion(GetString(root, "name"), tagName);
                var rid = CurrentPlatformRid();

                var asset = FindPlatformAsset(root, rid);
                if (asset == null)
                {
                    return NotFound(new { error = $"Release '{tagName}' publishes no package for this platform ({rid})." });
                }

                var directory = Path.Combine(
                    Directory.GetCurrentDirectory(), UpdateRootDirName, SafePathSegment(version));
                Directory.CreateDirectory(directory);
                var savePath = Path.Combine(directory, asset.Value.Name);

                if (!req.Force
                    && System.IO.File.Exists(savePath)
                    && new FileInfo(savePath).Length == asset.Value.Size
                    && asset.Value.Size > 0)
                {
                    return Ok(BuildDownloadResult(version, rid, asset.Value, savePath, alreadyDownloaded: true));
                }

                // Asset downloads go to release-assets, not the API, so they need their own long timeout.
                using var downloadClient = CreateClient();
                downloadClient.Timeout = TimeSpan.FromMinutes(10);
                using (var response = await downloadClient.GetAsync(asset.Value.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync();
                    await using var destination = System.IO.File.Create(savePath);
                    await source.CopyToAsync(destination);
                }

                _logger.LogInformation("UpdateDownloaded {File} to {Path}", asset.Value.Name, savePath);
                return Ok(BuildDownloadResult(version, rid, asset.Value, savePath, alreadyDownloaded: false));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateGitHub download failed");
                return StatusCode(502, new { error = ex.Message });
            }
        }

        private HttpClient CreateClient()
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Empostor-UpdateCheck/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            if (_gitHubToken.Length > 0)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _gitHubToken);
            }

            return client;
        }

        /// <summary>
        ///     Fetches a GitHub API URL, caching successes and turning rate limiting into a structured
        ///     error instead of a bare <see cref="HttpRequestException"/> that loses the response headers.
        /// </summary>
        private async Task<GitHubResponse> FetchAsync(HttpClient client, string url)
        {
            if (ResponseCache.TryGetValue(url, out var cached) && cached.Expiry > DateTimeOffset.UtcNow)
            {
                return new GitHubResponse(cached.Json, null, false, null);
            }

            using var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                ResponseCache[url] = (DateTimeOffset.UtcNow.Add(CacheLifetime), body);
                return new GitHubResponse(body, null, false, null);
            }

            var status = (int)response.StatusCode;
            var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var r) ? r.FirstOrDefault() : null;
            var resetHeader = response.Headers.TryGetValues("X-RateLimit-Reset", out var s) ? s.FirstOrDefault() : null;
            DateTimeOffset? resetAt = long.TryParse(resetHeader, out var epoch)
                ? DateTimeOffset.FromUnixTimeSeconds(epoch)
                : null;

            var rateLimited = status == 403 && remaining == "0";
            var detail = rateLimited
                ? _gitHubToken.Length == 0
                    ? "GitHub API rate limit exceeded: anonymous requests are capped at 60 per hour per IP. Set Admin.GitHubToken in config.json to raise it to 5000."
                    : "GitHub API rate limit exceeded for the configured token (5000 per hour)."
                : $"GitHub API returned {status}.";

            _logger.LogWarning(
                "UpdateGitHub {Url} failed: {Status}{Limited}", url, status, rateLimited ? " (rate limited)" : string.Empty);
            return new GitHubResponse(null, detail, rateLimited, resetAt);
        }

        private ObjectResult GitHubError(GitHubResponse response) => StatusCode(502, new
        {
            error = response.Error,
            rateLimited = response.RateLimited,
            resetAt = response.ResetAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            authenticated = _gitHubToken.Length > 0,
        });

        private sealed record GitHubResponse(string? Json, string? Error, bool RateLimited, DateTimeOffset? ResetAt)
        {
            public bool Ok => Json != null;
        }

        private static (string Mode, string Target, string? Error) ResolveChannel(string? channel, string? tag)
        {
            var mode = string.IsNullOrWhiteSpace(channel) ? ChannelStable : channel.Trim().ToLowerInvariant();
            var target = (tag ?? string.Empty).Trim();

            if (mode != ChannelStable && mode != ChannelNightly && mode != ChannelTag)
            {
                return (mode, target, $"Unknown release channel '{channel}'.");
            }

            if (mode == ChannelTag && !TagNamePattern.IsMatch(target))
            {
                return (mode, target, "A valid release tag is required.");
            }

            return (mode, target, null);
        }

        /// <summary>Portable RID of the running server, mapped onto the names build.cake publishes.</summary>
        private static string CurrentPlatformRid()
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            if (SupportedRids.Contains(rid, StringComparer.OrdinalIgnoreCase))
            {
                return rid.ToLowerInvariant();
            }

            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                _ => "x64",
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-" + arch;
            }

            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx-" + arch : "linux-" + arch;
        }

        private static (string Name, string Url, long Size)? FindPlatformAsset(JsonElement release, string rid)
        {
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(string Name, string Url, long Size)>();
            foreach (var asset in assets.EnumerateArray())
            {
                var name = GetString(asset, "name");
                if (!IsPlatformAsset(name, rid))
                {
                    continue;
                }

                var size = asset.TryGetProperty("size", out var raw) && raw.TryGetInt64(out var parsed) ? parsed : 0;
                candidates.Add((name, GetString(asset, "browser_download_url"), size));
            }

            // Windows ships .zip, the other platforms only .tar.gz; prefer the zip when both exist.
            foreach (var candidate in candidates)
            {
                if (candidate.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return candidates.Count > 0 ? candidates[0] : null;
        }

        /// <summary>Tagged releases use "_" before the RID, nightly uses "-"; accept both.</summary>
        private static bool IsPlatformAsset(string name, string rid)
        {
            foreach (var separator in new[] { "-", "_" })
            {
                if (name.EndsWith(separator + rid + ".zip", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(separator + rid + ".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Keeps a version usable as a single directory name.</summary>
        private static string SafePathSegment(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '+' ? c : '_');
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static object BuildDownloadResult(
            string version, string rid, (string Name, string Url, long Size) asset, string savePath, bool alreadyDownloaded)
        {
            var relative = Path.Combine(UpdateRootDirName, SafePathSegment(version), asset.Name).Replace('\\', '/');
            return new
            {
                version,
                platform = rid,
                fileName = asset.Name,
                savedTo = savePath,
                relativePath = relative,
                size = System.IO.File.Exists(savePath) ? new FileInfo(savePath).Length : asset.Size,
                alreadyDownloaded,
            };
        }

        private static string ReleaseApiUrl(string mode, string tag) => mode switch
        {
            ChannelNightly => $"https://api.github.com/repos/{EmpostorRepo}/releases/tags/{ChannelNightly}",
            ChannelTag => $"https://api.github.com/repos/{EmpostorRepo}/releases/tags/{Uri.EscapeDataString(tag)}",
            _ => $"https://api.github.com/repos/{EmpostorRepo}/releases/latest",
        };

        /// <summary>Nightly builds carry their real version inside the release title, e.g. "Nightly build (2.1.0-ci.812)".</summary>
        private static string ExtractVersion(string? name, string tag)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var match = VersionInName.Match(name);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return tag.TrimStart('v');
        }

        private static string GetString(JsonElement element, string property)
            => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static bool IsTrue(JsonElement element, string property)
            => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

        public sealed record UpdateDownloadRequest(string? Channel = null, string? Tag = null, bool Force = false);
    }
}

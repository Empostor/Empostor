using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Api.Net;
using Empostor.Api.Net.Manager;
using Empostor.Server.Service.Admin.Ban;
using Empostor.Server.Service.Admin.Chat;
using Empostor.Server.Service.Admin.Reactor;
using Empostor.Server.Service.Admin.Report;
using Empostor.Server.Service.Stat;
using Empostor.Server.Service.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http
{
    [ApiController]
    public sealed class AdminController : ControllerBase
    {
        private static readonly DateTime StartTime = DateTime.UtcNow;
        private static Dictionary<string, string>? _adminStrings;
        private static readonly object _stringsLock = new();

        private const int LoginLockoutThreshold = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);

        private readonly ILogger<AdminController> _logger;
        private readonly IGameManager _gameManager;
        private readonly IClientManager _clientManager;
        private readonly BanStore _bans;
        private readonly AdminConfig _config;
        private readonly ReportStore _reportStore;
        private readonly PlayerLogStore _playerLogs;
        private readonly PlayerStatsStore _playerStats;
        private readonly PlayerStatsConfig _statsConfig;
        private readonly ChatFilterStore _chatFilter;
        private readonly DiscordWebhookStore _discordWebhook;
        private readonly string _passwordHash;
        private static readonly ConcurrentDictionary<string, (int Count, DateTime FirstAttempt)> _loginFailures = new();

        public AdminController(
            ILogger<AdminController> logger,
            IGameManager gameManager,
            IClientManager clientManager,
            BanStore bans,
            IOptions<AdminConfig> config,
            ReportStore reportStore,
            PlayerLogStore playerLogs,
            PlayerStatsStore playerStats,
            IOptions<PlayerStatsConfig> statsConfig,
            ChatFilterStore chatFilter,
            DiscordWebhookStore discordWebhook)
        {
            _logger = logger;
            _gameManager = gameManager;
            _clientManager = clientManager;
            _bans = bans;
            _config = config.Value;
            _reportStore = reportStore;
            _playerLogs = playerLogs;
            _playerStats = playerStats;
            _statsConfig = statsConfig.Value;
            _chatFilter = chatFilter;
            _discordWebhook = discordWebhook;
            _passwordHash = AdminAuthHelper.ComputeHash(_config.Password);

            if (string.IsNullOrEmpty(_config.Password)
                || _config.Password == "CHANGE-ME"
                || _config.Password == "admin123")
            {
                _logger.LogWarning(
                    "[Admin] SECURITY: Default or empty admin password detected! Set a strong password in config.json -> Admin.Password to protect the admin panel.");
            }
        }

        private static Dictionary<string, string> LoadStrings()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "AdminStrings.json");
            if (!System.IO.File.Exists(path))
            {
                return new Dictionary<string, string>();
            }

            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }

        private bool IsAuthenticated()
            => Request.Cookies.TryGetValue("empostor_admin", out var v)
               && AdminAuthHelper.ConstantTimeEquals(v, _passwordHash);

        [HttpGet("/admin")]
        public IActionResult Panel()
            => Content(IsAuthenticated() ? AdminHtml : LoginHtml, "text/html; charset=utf-8");

        [HttpPost("/admin/login")]
        public IActionResult Login([FromForm] string password)
        {
            var ip = GetClientIp()?.ToString() ?? IPAddress.Loopback.ToString();

            // Rate-limit check: block IPs that have failed too many times recently
            if (_loginFailures.TryGetValue(ip, out var entry))
            {
                if (entry.Count >= LoginLockoutThreshold
                    && DateTime.UtcNow - entry.FirstAttempt < LoginLockoutDuration)
                {
                    var remaining = (int)(LoginLockoutDuration - (DateTime.UtcNow - entry.FirstAttempt)).TotalMinutes;
                    _logger.LogWarning("[Admin] Rate-limited login attempt from {Ip} (locked out for ~{Min}m more)", ip, Math.Max(1, remaining));
                    return Content(LoginHtml.Replace("<!--ERR-->",
                        $"<p style='color:var(--r);margin-top:8px'>Too many failed attempts. Try again in {Math.Max(1, remaining)} minute(s).</p>"),
                        "text/html; charset=utf-8");
                }

                // Reset if lockout window has passed
                if (DateTime.UtcNow - entry.FirstAttempt >= LoginLockoutDuration)
                {
                    _loginFailures.TryRemove(ip, out _);
                }
            }

            var submittedHash = AdminAuthHelper.ComputeHash(password);

            if (!AdminAuthHelper.ConstantTimeEquals(submittedHash, _passwordHash))
            {
                _loginFailures.AddOrUpdate(ip,
                    _ => (1, DateTime.UtcNow),
                    (_, e) => (e.Count + 1, e.FirstAttempt));

                _logger.LogWarning("[Admin] Failed login from {Ip} (attempt {Count})", ip,
                    _loginFailures.TryGetValue(ip, out var updated) ? updated.Count : 1);
                return Content(LoginHtml.Replace("<!--ERR-->",
                    "<p style='color:var(--r);margin-top:8px'>Incorrect password.</p>"),
                    "text/html; charset=utf-8");
            }

            // Successful login — clear rate-limit entry for this IP
            _loginFailures.TryRemove(ip, out _);

            Response.Cookies.Append("empostor_admin", _passwordHash, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(8),
            });
            return Redirect("/admin");
        }

        [HttpPost("/admin/logout")]
        public IActionResult Logout() { Response.Cookies.Delete("empostor_admin"); return Redirect("/admin"); }

        [HttpGet("/api/admin/strings")]
        public IActionResult GetStrings()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var strings = LoadStrings();
            lock (_stringsLock) { _adminStrings = strings; }
            return Ok(strings);
        }

        [HttpPost("/api/admin/strings/reload")]
        public IActionResult ReloadStrings()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var strings = LoadStrings();
            lock (_stringsLock) { _adminStrings = strings; }
            return Ok(strings);
        }

        [HttpGet("/api/admin/status")]
        public IActionResult Status()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var up = DateTime.UtcNow - StartTime;
            var games = _gameManager.Games.ToList();
            var (ib, fb) = _bans.Stats();
            return Ok(new
            {
                uptime = Fmt(up),
                uptimeSeconds = (long)up.TotalSeconds,
                startTime = StartTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                totalGames = games.Count,
                totalPlayers = _clientManager.Clients.Count(),
                publicGames = games.Count(g => g.IsPublic),
                activeGames = games.Count(g => g.GameState == GameStates.Started),
                bannedIps = ib,
                bannedFriendCodes = fb,
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                pid = Environment.ProcessId,
            });
        }

        [HttpGet("/api/admin/games")]
        public IActionResult GetGames()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(_gameManager.Games.Select(Snap));
        }

        [HttpGet("/api/admin/clients")]
        public IActionResult GetClients()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(_clientManager.Clients.Select(CSnap));
        }

        [HttpGet("/api/admin/bans")]
        public IActionResult GetBans()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(new { ips = _bans.AllIpBans(), friendCodes = _bans.AllFriendCodeBans() });
        }

        [HttpPost("/api/admin/broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.Message))
            {
                return BadRequest(Err("Message required"));
            }

            var sent = 0;
            foreach (var g in _gameManager.Games)
            {
                var host = g.Host?.Character;
                if (host != null) { await host.SendChatAsync($"[Server] {req.Message}"); sent++; }
            }

            return Ok(new { sent });
        }

        [HttpPost("/api/admin/message")]
        public async Task<IActionResult> GameMessage([FromBody] GameMsgReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var game = FindGame(req.GameCode);
            if (game == null)
            {
                return NotFound(Err($"Game '{req.GameCode}' not found"));
            }

            var host = game.Host?.Character;
            if (host == null)
            {
                return BadRequest(Err("No host character"));
            }

            await host.SendChatAsync($"[Admin] {req.Message}");
            return Ok(new { ok = true });
        }

        [HttpPost("/api/admin/kick")]
        public async Task<IActionResult> Kick([FromBody] ClientIdReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var c = FindClient(req.ClientId);
            if (c == null)
            {
                return NotFound(Err($"Client {req.ClientId} not found"));
            }

            await c.DisconnectAsync(DisconnectReason.Custom, req.Reason ?? "Kicked by admin");

            if (c.Player != null)
            {
                await c.Player.KickAsync();
            }

            return Ok(new { kicked = true, name = c.Name });
        }

        [HttpPost("/api/admin/ban/ip")]
        public async Task<IActionResult> BanIp([FromBody] BanIpReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (!IPAddress.TryParse(req.Ip, out var ip))
            {
                return BadRequest(Err("Invalid IP"));
            }

            var entry = _bans.BanIp(ip, req.Reason ?? "Banned by admin");
            var kicked = 0;
            foreach (var c in _clientManager.Clients.ToList())
            {
                var cip = c.Connection?.EndPoint?.Address;
                if (cip != null && Norm(cip) == Norm(ip))
                {
                    await c.DisconnectAsync(DisconnectReason.Custom, req.Reason ?? "Banned by admin");

                    if (c.Player != null)
                    {
                        await c.Player.BanAsync();
                    }

                    kicked++;
                }
            }

            return Ok(new { banned = entry.Value, disconnected = kicked });
        }

        [HttpPost("/api/admin/ban/fc")]
        public async Task<IActionResult> BanFc([FromBody] BanFcReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.FriendCode))
            {
                return BadRequest(Err("FriendCode required"));
            }

            var entry = _bans.BanFriendCode(req.FriendCode, req.Reason ?? "Banned by admin");
            var kicked = 0;
            foreach (var c in _clientManager.Clients.ToList())
            {
                if (c.FriendCode == req.FriendCode)
                {
                    await c.DisconnectAsync(DisconnectReason.Custom, req.Reason ?? "Banned by admin");

                    if (c.Player != null)
                    {
                        await c.Player.BanAsync();
                    }

                    kicked++;
                }
            }

            return Ok(new { banned = entry.Value, disconnected = kicked });
        }

        [HttpPost("/api/admin/unban/ip")]
        public IActionResult UnbanIp([FromBody] UnbanReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(new { removed = _bans.UnbanIp(req.Value) });
        }

        [HttpPost("/api/admin/unban/fc")]
        public IActionResult UnbanFc([FromBody] UnbanReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(new { removed = _bans.UnbanFriendCode(req.Value) });
        }

        [HttpPost("/api/admin/game/end")]
        public async Task<IActionResult> EndGame([FromBody] GameCodeReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var g = FindGame(req.GameCode);
            if (g == null)
            {
                return NotFound(Err($"Game '{req.GameCode}' not found"));
            }

            var players = g.Players.ToList();
            foreach (var p in players)
            {
                await p.Client.DisconnectAsync(DisconnectReason.Custom, req.Reason ?? "Game ended by admin");
                await p.KickAsync();
            }

            return Ok(new { ended = req.GameCode, playersKicked = players.Count });
        }

        [HttpPost("/api/admin/game/public")]
        public async Task<IActionResult> SetPublic([FromBody] GamePublicReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var g = FindGame(req.GameCode);
            if (g == null)
            {
                return NotFound(Err($"Game '{req.GameCode}' not found"));
            }

            await g.SetPrivacyAsync(req.IsPublic);
            return Ok(new { gameCode = req.GameCode, isPublic = req.IsPublic });
        }

        [HttpGet("/api/admin/reports")]
        public IActionResult GetReports()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(_reportStore.GetRecent(200).Select(r => new
            {
                time = r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                gameCode = r.GameCode,
                reporterName = r.ReporterName,
                reporterFc = r.ReporterFriendCode ?? "—",
                reportedName = r.ReportedName ?? "—",
                reportedFc = r.ReportedFriendCode ?? "—",
                reason = r.Reason.ToString(),
                outcome = r.Outcome.ToString(),
            }));
        }

        [HttpGet("/api/admin/player/logs/clients")]
        public IActionResult GetPlayerLogClients()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(_playerLogs.GetLoggedClientIds().Select(id => new
            {
                clientId = id,
                name = FindClient(id)?.Name ?? "Disconnected",
                friendCode = FindClient(id)?.FriendCode ?? "—",
            }));
        }

        [HttpGet("/api/admin/player/logs")]
        public IActionResult GetPlayerLogs([FromQuery] int? clientId)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var logs = clientId.HasValue
                ? _playerLogs.GetByClient(clientId.Value)
                : _playerLogs.GetAll();
            return Ok(logs.Select(e => new
            {
                time = e.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                type = e.Type,
                clientId = e.ClientId,
                playerName = e.PlayerName ?? "—",
                friendCode = e.FriendCode ?? "—",
                gameCode = e.GameCode ?? "—",
                detail = e.Detail ?? "—",
            }));
        }

        [HttpGet("/api/admin/player/logs/export")]
        public IActionResult ExportPlayerLogs([FromQuery] int? clientId)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var data = clientId.HasValue
                ? _playerLogs.ExportJson(clientId.Value)
                : _playerLogs.ExportJson();
            var name = clientId.HasValue
                ? $"player_{clientId}_logs.json"
                : "all_player_logs.json";
            return File(data, "application/json; charset=utf-8", name);
        }

        [HttpGet("/api/admin/player/stats")]
        public IActionResult GetPlayerStats()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (!_statsConfig.Enabled)
            {
                return Ok(new { enabled = false });
            }

            return Ok(new
            {
                enabled = true,
                players = _playerStats.GetAll().Select(s => new
                {
                    friendCode = s.FriendCode,
                    name = s.LastKnownName ?? "—",
                    gamesPlayed = s.GamesPlayed,
                    wins = s.Wins,
                    losses = s.Losses,
                    impostorWins = s.ImpostorWins,
                    kills = s.Kills,
                    deaths = s.Deaths,
                    tasksCompleted = s.TasksCompleted,
                    timesExiled = s.TimesExiled,
                    firstSeen = s.FirstSeen.ToString("yyyy-MM-dd HH:mm:ss"),
                    lastSeen = s.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"),
                }),
            });
        }

        [HttpGet("/api/admin/player/stats/{friendCode}")]
        public IActionResult GetPlayerStat(string friendCode)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            var s = _playerStats.GetByFriendCode(friendCode);
            if (s == null)
            {
                return NotFound(Err("Player not found"));
            }

            return Ok(new
            {
                friendCode = s.FriendCode,
                name = s.LastKnownName ?? "—",
                gamesPlayed = s.GamesPlayed,
                wins = s.Wins,
                losses = s.Losses,
                impostorWins = s.ImpostorWins,
                kills = s.Kills,
                deaths = s.Deaths,
                tasksCompleted = s.TasksCompleted,
                timesExiled = s.TimesExiled,
                firstSeen = s.FirstSeen.ToString("yyyy-MM-dd HH:mm:ss"),
                lastSeen = s.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }

        [HttpPost("/api/admin/player/stats/reset")]
        public IActionResult ResetPlayerStats()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            _playerStats.ClearAll();
            return Ok(new { reset = true });
        }

        [HttpGet("/api/admin/chatfilter")]
        public IActionResult GetChatFilter()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(new
            {
                enabled = _chatFilter.Enabled,
                blockedWords = _chatFilter.BlockedWords,
                blockMessage = _chatFilter.BlockMessage,
                spamThreshold = _chatFilter.SpamThreshold,
                spamWindowSeconds = _chatFilter.SpamWindowSeconds,
            });
        }

        [HttpPost("/api/admin/chatfilter/words/add")]
        public IActionResult AddChatFilterWord([FromBody] ChatFilterWordReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.Word))
            {
                return BadRequest(Err("Word required"));
            }

            _chatFilter.AddWord(req.Word);
            return Ok(new { blockedWords = _chatFilter.BlockedWords });
        }

        [HttpPost("/api/admin/chatfilter/words/remove")]
        public IActionResult RemoveChatFilterWord([FromBody] ChatFilterWordReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.Word))
            {
                return BadRequest(Err("Word required"));
            }

            _chatFilter.RemoveWord(req.Word);
            return Ok(new { blockedWords = _chatFilter.BlockedWords });
        }

        [HttpPost("/api/admin/chatfilter/settings")]
        public IActionResult UpdateChatFilterSettings([FromBody] ChatFilterSettingsReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (req.Enabled.HasValue)
            {
                _chatFilter.Enabled = req.Enabled.Value;
            }

            if (req.BlockMessage.HasValue)
            {
                _chatFilter.BlockMessage = req.BlockMessage.Value;
            }

            if (req.SpamThreshold.HasValue)
            {
                _chatFilter.SpamThreshold = req.SpamThreshold.Value;
            }

            if (req.SpamWindowSeconds.HasValue)
            {
                _chatFilter.SpamWindowSeconds = req.SpamWindowSeconds.Value;
            }

            return Ok(new
            {
                enabled = _chatFilter.Enabled,
                blockedWords = _chatFilter.BlockedWords,
                blockMessage = _chatFilter.BlockMessage,
                spamThreshold = _chatFilter.SpamThreshold,
                spamWindowSeconds = _chatFilter.SpamWindowSeconds,
            });
        }

        [HttpGet("/api/admin/discord")]
        public IActionResult GetDiscordWebhook()
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            return Ok(new
            {
                enabled = _discordWebhook.Enabled,
                webhookUrl = _discordWebhook.WebhookUrl,
                notifyOnGameCreated = _discordWebhook.NotifyOnGameCreated,
                notifyOnBan = _discordWebhook.NotifyOnBan,
                notifyOnReport = _discordWebhook.NotifyOnReport,
                notifyOnPlayerJoin = _discordWebhook.NotifyOnPlayerJoin,
                notifyOnGameEnded = _discordWebhook.NotifyOnGameEnded,
            });
        }

        [HttpPost("/api/admin/discord")]
        public async Task<IActionResult> UpdateDiscordWebhook([FromBody] DiscordWebhookSettingsReq req)
        {
            if (!IsAuthenticated())
            {
                return Unauthorized();
            }

            if (req.Enabled.HasValue)
            {
                _discordWebhook.Enabled = req.Enabled.Value;
            }

            if (req.WebhookUrl != null)
            {
                _discordWebhook.WebhookUrl = req.WebhookUrl;
            }

            if (req.NotifyOnGameCreated.HasValue)
            {
                _discordWebhook.NotifyOnGameCreated = req.NotifyOnGameCreated.Value;
            }

            if (req.NotifyOnBan.HasValue)
            {
                _discordWebhook.NotifyOnBan = req.NotifyOnBan.Value;
            }

            if (req.NotifyOnReport.HasValue)
            {
                _discordWebhook.NotifyOnReport = req.NotifyOnReport.Value;
            }

            if (req.NotifyOnPlayerJoin.HasValue)
            {
                _discordWebhook.NotifyOnPlayerJoin = req.NotifyOnPlayerJoin.Value;
            }

            if (req.NotifyOnGameEnded.HasValue)
            {
                _discordWebhook.NotifyOnGameEnded = req.NotifyOnGameEnded.Value;
            }

            await _discordWebhook.SaveAsync();

            return Ok(new
            {
                enabled = _discordWebhook.Enabled,
                webhookUrl = _discordWebhook.WebhookUrl,
                notifyOnGameCreated = _discordWebhook.NotifyOnGameCreated,
                notifyOnBan = _discordWebhook.NotifyOnBan,
                notifyOnReport = _discordWebhook.NotifyOnReport,
                notifyOnPlayerJoin = _discordWebhook.NotifyOnPlayerJoin,
                notifyOnGameEnded = _discordWebhook.NotifyOnGameEnded,
            });
        }

        private IGame? FindGame(string code)
        {
            try { return _gameManager.Find(new GameCode(code.ToUpperInvariant())); }
            catch { return null; }
        }

        private IClient? FindClient(int id) => _clientManager.Clients.FirstOrDefault(c => c.Id == id);

        private static object Snap(IGame g) => new
        {
            code = GameCodeParser.IntToGameName(g.Code),
            state = g.GameState.ToString(),
            isPublic = g.IsPublic,
            playerCount = g.PlayerCount,
            maxPlayers = g.Options.MaxPlayers,
            map = g.Options.Map.ToString(),
            impostors = g.Options.NumImpostors,
            host = g.Host?.Client.Name ?? "—",
            hostFc = g.Host?.Client.FriendCode ?? "—",
            players = g.Players.Select(p => new
            {
                id = p.Client.Id,
                name = p.Client.Name,
                friendCode = p.Client.FriendCode ?? "—",
                isHost = p.IsHost,
                platform = p.Client.PlatformSpecificData?.Platform.ToString() ?? "Unknown",
                ip = p.Client.Connection?.EndPoint?.Address?.ToString() ?? "—",
            }).ToList(),
        };

        private static object CSnap(IClient c)
        {
            var reactor = c.GetReactorMods();
            return new
            {
                id = c.Id,
                name = c.Name,
                friendCode = c.FriendCode ?? "—",
                gameVersion = c.GameVersion.ToString(),
                platform = c.PlatformSpecificData?.Platform.ToString() ?? "Unknown",
                inGame = c.Player != null,
                gameCode = c.Player != null ? GameCodeParser.IntToGameName(c.Player.Game.Code) : "—",
                ip = c.Connection?.EndPoint?.Address?.ToString() ?? "—",
                reactor = reactor == null ? null : new
                {
                    protocolVersion = reactor.ProtocolVersion,
                    mods = System.Linq.Enumerable.Select(reactor.Mods, m => new
                    {
                        id = m.Id,
                        version = m.Version,
                        required = m.RequiredOnAllClients,
                    }).ToArray(),
                },
            };
        }

        private static string Norm(IPAddress ip)
            => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

        private IPAddress? GetClientIp()
        {
            // Check reverse-proxy headers first so we get the real client IP,
            // not the CDN/nginx IP. Using Connection.RemoteIpAddress directly
            // would rate-limit or ban the proxy instead of the actual user.
            var xRealIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp) && IPAddress.TryParse(xRealIp, out var realIp))
            {
                return Normalize(realIp);
            }

            var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                var first = xForwardedFor.Split(',')[0].Trim();
                if (IPAddress.TryParse(first, out var fwdIp))
                {
                    return Normalize(fwdIp);
                }
            }

            return Normalize(HttpContext.Connection.RemoteIpAddress);
        }

        private static IPAddress? Normalize(IPAddress? ip)
            => ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip;

        private static object Err(string msg) => new { error = msg };

        private static string Fmt(TimeSpan t)
        {
            if (t.TotalDays >= 1)
            {
                return $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m";
            }

            if (t.TotalHours >= 1)
            {
                return $"{t.Hours}h {t.Minutes}m {t.Seconds}s";
            }

            return $"{t.Minutes}m {t.Seconds}s";
        }

        public sealed record BroadcastReq(string Message);

        public sealed record GameMsgReq(string GameCode, string Message);

        public sealed record ClientIdReq(int ClientId, string? Reason = null);

        public sealed record BanIpReq(string Ip, string? Reason);

        public sealed record BanFcReq(string FriendCode, string? Reason);

        public sealed record UnbanReq(string Value);

        public sealed record GameCodeReq(string GameCode, string? Reason = null);

        public sealed record GamePublicReq(string GameCode, bool IsPublic);

        public sealed record ChatFilterWordReq(string Word);

        public sealed record ChatFilterSettingsReq(bool? Enabled, bool? BlockMessage, int? SpamThreshold, int? SpamWindowSeconds);

        public sealed record DiscordWebhookSettingsReq(bool? Enabled, string? WebhookUrl, bool? NotifyOnGameCreated, bool? NotifyOnBan, bool? NotifyOnReport, bool? NotifyOnPlayerJoin, bool? NotifyOnGameEnded);

        // HTML pages are auto-generated to BaseDirectory/Pages/ on first access.
        // Server operators can customize them by editing the files on disk.
        private static string? _loginHtml;
        private static string? _adminHtml;

        private string LoginHtml => _loginHtml ??= GetPage("login.html", AdminTemplateDefaults.LoginHtml);
        private string AdminHtml => _adminHtml ??= GetPage("admin.html", AdminTemplateDefaults.AdminHtml);

        private static string GetPage(string fileName, string defaultContent)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages");
                var fullPath = Path.Combine(dir, fileName);

                if (!System.IO.File.Exists(fullPath))
                {
                    Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllText(fullPath, defaultContent);
                }

                return System.IO.File.ReadAllText(fullPath);
            }
            catch
            {
                return defaultContent;
            }
        }


    }
}

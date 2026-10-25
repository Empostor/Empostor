using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Empostor.Api.Config;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Api.Innersloth;
using Empostor.Api.Innersloth.GameFilters;
using Empostor.Server.Extensions;
using Empostor.Server.Net;
using Empostor.Server.Service.Auth;
using Empostor.Server.Service.Ip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http;

[Route("/api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameManager _gameManager;
    private readonly ListingManager _listingManager;
    private readonly ServerConfig _serverConfig;
    private readonly AuthCacheService _authCache;
    private readonly GameCodeAuthManager _gameCodeAuth;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        IGameManager gameManager,
        ListingManager listingManager,
        IOptions<ServerConfig> serverConfig,
        AuthCacheService authCache,
        GameCodeAuthManager gameCodeAuth,
        ILogger<GamesController> logger)
    {
        _gameManager = gameManager;
        _listingManager = listingManager;
        _serverConfig = serverConfig.Value;
        _authCache = authCache;
        _gameCodeAuth = gameCodeAuth;
        _logger = logger;
    }

    /// <summary>
    /// Legacy game list endpoint (Among Us &lt; 16.0.0).
    /// </summary>
    [HttpGet]
    public IActionResult Index(int mapId, GameKeywords lang, int numImpostors,
        [FromHeader] AuthenticationHeaderValue authorization)
    {
        if (authorization?.Scheme != "Bearer" || authorization.Parameter == null)
        {
            return BadRequest();
        }

        // Parse client version from token if available; fall back to known recent version
        GameVersion clientVersion;
        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(authorization.Parameter));
            var root = doc.RootElement;
            var ver = root.TryGetProperty("Content", out var c)
                      && c.TryGetProperty("ClientVersion", out var cv)
                ? cv.GetInt32() : 0;
            clientVersion = ver > 0 ? new GameVersion(ver) : new GameVersion(2021, 6, 30);
        }
        catch
        {
            clientVersion = new GameVersion(2021, 6, 30);
        }

        var listings = _listingManager.FindListings(HttpContext, mapId, numImpostors, lang, clientVersion);
        var auth = TryGetAuthInfo();
        return Ok(listings.Select(g => BuildListing(g, auth)));
    }

    /// <summary>
    /// Legacy: get server hosting a specific game.
    /// </summary>
    [HttpPost]
    public IActionResult Post(int gameId)
    {
        var requested = new GameCode(gameId);
        var code = _gameCodeAuth.TryGetTarget(requested, out var targetCode) ? targetCode : requested;
        var game = _gameManager.Find(code);
        if (game == null)
        {
            return NotFound(new MatchmakerResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        if (_serverConfig.UseGameCodeMode && TryGetAuthInfo() is { } auth)
        {
            // The legacy path cannot return a code, so stash this as a pending join addressed to the real code.
            _gameCodeAuth.RegisterPendingJoin(code, auth);
        }

        var port = GetPort();
        return Ok(HostServer.From(IPAddress.Parse(_serverConfig.ResolvePublicIp()), port));
    }

    /// <summary>
    /// Legacy: get server address to host a new game.
    /// </summary>
    [HttpPut]
    public IActionResult Put()
    {
        if (_serverConfig.UseGameCodeMode && TryGetAuthInfo() is { } auth)
        {
            // Hosting starts here. The game is created by the next UDP HostGame, so keep the auth info and IP pending.
            var ip = RealIpResolver.Resolve(HttpContext)?.ToString();
            _gameCodeAuth.RegisterHost(auth, ip);
        }

        var port = GetPort();
        return Ok(HostServer.From(IPAddress.Parse(_serverConfig.ResolvePublicIp()), port));
    }

    [HttpGet("{gameId}")]
    public IActionResult Show([FromRoute] int gameId)
    {
        // The submitted code may be a real code (invite or manual input) or a challenge code.
        var requested = new GameCode(gameId);
        var code = _gameCodeAuth.TryGetTarget(requested, out var targetCode) ? targetCode : requested;
        var game = _gameManager.Find(code);
        if (game == null)
        {
            return NotFound(new FindGameByCodeResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        return Ok(new FindGameByCodeResponse(BuildListing(game, TryGetAuthInfo())));
    }

    /// <summary>
    /// Filtered lobby list (Among Us 16.0.0+).
    /// </summary>
    [HttpGet("filtered")]
    public IActionResult ShowFilteredLobbies([FromQuery] string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.ServerError, "filter query parameter not provided")));
        }

        try
        {
            var decodedFilter = HttpUtility.UrlDecode(filter);
            var filtersList = JsonSerializer.Deserialize<GameFiltersList>(decodedFilter);

            // filterSets wont be null. It must at least have ChatFilter and LangFilter
            // Vanilla game only builds one filterSet and InnerSloth officials only handles first one (though you can send multiple filter sets. sloths only handle the first)
            if (filtersList == null || filtersList.FilterSets.Count != 1
                || filtersList.FilterSets[0].Filters.Count < 2
                || !filtersList.FilterSets[0].Filters.Any(x => x.OptionType == "languages")
                || !filtersList.FilterSets[0].Filters.Any(x => x.OptionType == "chat"))
            {
                return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.ServerError, "Invalid filterSets")));
            }

            var filteredGames = _listingManager.FindListingsV2(HttpContext, filtersList);
            var auth = TryGetAuthInfo();
            var gameListings = filteredGames.Select(g => BuildListing(g, auth)).ToList();

            var response = new
            {
                games = gameListings,
                metadata = new
                {
                    allGamesCount = _gameManager.Games.Count(),
                    matchingGamesCount = gameListings.Count,
                },
            };

            return Ok(response);
        }
        catch (JsonException ex)
        {
            return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.ServerError, "Unable to deserialize filter json" + ex)));
        }
        catch (Exception ex)
        {
            return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.ServerError, "Unknown exception caught in filter" + ex)));
        }
    }

    /// <summary>
    /// JSON summary of all active games, consumed by the admin panel.
    /// Does not filter by IsPublic; rooms with a Note include the note field.
    /// </summary>
    [HttpGet("publicgames")]
    public IActionResult Summary()
    {
        var games = _gameManager.Games.Select(g => new
        {
            code = GameCodeParser.IntToGameName(g.Code),
            codeInt = (int)g.Code,
            state = g.GameState.ToString(),
            isPublic = g.IsPublic,
            playerCount = g.PlayerCount,
            maxPlayers = g.Options.MaxPlayers,
            map = g.Options.Map.ToString(),
            impostors = g.Options.NumImpostors,
            host = g.Host?.Client.Name ?? "UnknwonName",
            hostFriendCode = g.Host?.Client.FriendCode ?? "UnknownHostFriendCode",
            note = string.IsNullOrEmpty(g.Note) ? null : g.Note,
            players = g.Players.Select(p => new
            {
                name = p.Client.Name,
                friendCode = p.Client.FriendCode ?? "UnknownFriendCode",
                isHost = p.IsHost,
                platform = p.Client.PlatformSpecificData?.PlatformName ?? "UnknownPlatform",
            }).ToList(),
        }).ToList();

        var result = new
        {
            totalGames = games.Count,
            totalPlayers = games.Sum(g => g.playerCount),
            games,
        };

        return new JsonResult(result, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    /// <summary>
    ///     The UDP port the client should connect to.
    ///     In GameCode mode returns the shared port, in DeltaPort mode the dedicated port from the token.
    /// </summary>
    private ushort GetPort()
    {
        if (_serverConfig.UseGameCodeMode)
        {
            return _serverConfig.GameCodePublicPort;
        }

        var auth = HttpContext.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(Convert.FromBase64String(auth["Bearer ".Length..]));
                if (doc.RootElement.TryGetProperty("Port", out var portElement)
                    && portElement.TryGetInt32(out var port)
                    && port > 0)
                {
                    return (ushort)port;
                }
            }
            catch
            {
                // Fall back to the public port if the value is not base64 or is corrupted
            }
        }

        return _serverConfig.PublicPort;
    }

    // Looks up auth info from the bearer token.
    private UserAuthInfo? TryGetAuthInfo()
    {
        var header = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(header["Bearer ".Length..]));
            if (doc.RootElement.TryGetProperty("Hash", out var hashElement)
                && hashElement.GetString() is { Length: > 0 } hash)
            {
                return _authCache.FindByToken(hash);
            }
        }
        catch
        {
            // Treat a corrupted token as unauthenticated
        }

        return null;
    }

    // Builds a single listing entry. In GameCode mode the challenge code bound to the auth info is returned as the GameId.
    private GameListing BuildListing(IGame game, UserAuthInfo? auth)
    {
        var port = GetPort();
        var listingCode = game.Code;

        if (_serverConfig.UseGameCodeMode && auth != null && !game.Code.IsInvalid)
        {
            listingCode = _gameCodeAuth.CreateChallenge(game.Code, auth);
        }

        return GameListing.From(game, port, listingCode);
    }

    private static uint ConvertAddressToNumber(IPAddress address)
    {
#pragma warning disable CS0618
        return (uint)address.Address;
#pragma warning restore CS0618
    }

    private class HostServer
    {
        [JsonPropertyName("Ip")] public required long Ip { get; init; }

        [JsonPropertyName("Port")] public required ushort Port { get; init; }

        public static HostServer From(IPAddress ip, ushort port) =>
            new() { Ip = ConvertAddressToNumber(ip), Port = port };

        public static HostServer From(IPEndPoint ep) =>
            From(ep.Address, (ushort)ep.Port);
    }

    private class MatchmakerResponse
    {
        [SetsRequiredMembers]
        public MatchmakerResponse(MatchmakerError error) { Errors = new[] { error }; }

        [JsonPropertyName("Errors")]
        public required MatchmakerError[] Errors { get; init; }
    }

    private class MatchmakerError
    {
        [SetsRequiredMembers]
        public MatchmakerError(DisconnectReason reason, string message = "")
        {
            Reason = reason;
            Message = message;
        }

        [JsonPropertyName("Reason")]
        public required DisconnectReason Reason { get; init; }

        [JsonPropertyName("Message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public required string Message { get; init; } = string.Empty;
    }

    private class FindGameByCodeResponse
    {
        [SetsRequiredMembers]
        public FindGameByCodeResponse(MatchmakerError e) => (Errors, Game) = (new[] { e }, null);

        [SetsRequiredMembers]
        public FindGameByCodeResponse(GameListing g) => (Errors, Game) = (null, g);

        [JsonPropertyName("Errors")] public required MatchmakerError[]? Errors { get; init; }

        [JsonPropertyName("Game")] public required GameListing? Game { get; init; }
    }

    private class GameListing
    {
        [JsonPropertyName("IP")] public required uint Ip { get; init; }

        [JsonPropertyName("Port")] public required ushort Port { get; init; }

        [JsonPropertyName("GameId")] public required int GameId { get; init; }

        [JsonPropertyName("PlayerCount")] public required int PlayerCount { get; init; }

        [JsonPropertyName("HostName")] public required string HostName { get; init; }

        [JsonPropertyName("TrueHostName")] public required string TrueHostName { get; init; }

        [JsonPropertyName("HostPlatformName")] public required string HostPlatformName { get; init; }

        [JsonPropertyName("Platform")] public required Platforms Platform { get; init; }

        [JsonPropertyName("QuickChat")] public required QuickChatModes QuickChat { get; init; }

        [JsonPropertyName("Age")] public required int Age { get; init; }

        [JsonPropertyName("MaxPlayers")] public required int MaxPlayers { get; init; }

        [JsonPropertyName("NumImpostors")] public required int NumImpostors { get; init; }

        [JsonPropertyName("MapId")] public required MapTypes MapId { get; init; }

        [JsonPropertyName("Language")] public required GameKeywords Language { get; init; }

        [JsonPropertyName("Options")] public required string Options { get; init; }

        public static GameListing From(IGame game, ushort port, GameCode listingCode)
        {
            var platform = game.Host?.Client.PlatformSpecificData;
            return new GameListing
            {
                Ip = ConvertAddressToNumber(game.PublicIp.Address),
                Port = port,
                GameId = listingCode,
                PlayerCount = game.PlayerCount,
                HostName = game.DisplayName ?? game.Host?.Client.Name ?? "Unknown host",
                TrueHostName = game.DisplayName ?? game.Host?.Client.Name ?? "Unknown host",
                HostPlatformName = platform?.PlatformName ?? string.Empty,
                Platform = platform?.Platform ?? Platforms.Unknown,
                QuickChat = game.Host?.Client.ChatMode ?? QuickChatModes.QuickChatOnly,
                Age = 0,
                MaxPlayers = game.Options.MaxPlayers,
                NumImpostors = game.Options.NumImpostors,
                MapId = game.Options.Map,
                Language = game.Options.Keywords,
                Options = game.Options.ToBase64String(),
            };
        }
    }
}

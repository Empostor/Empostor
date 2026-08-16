using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Empostor.Api.Config;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Server.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http;

[Route("/api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameManager _gameManager;
    private readonly ListingManager _listingManager;
    private readonly ServerConfig _serverConfig;

    public GamesController(
        IGameManager gameManager,
        ListingManager listingManager,
        IOptions<ServerConfig> serverConfig)
    {
        _gameManager = gameManager;
        _listingManager = listingManager;
        _serverConfig = serverConfig.Value;
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
        var port = GetDeltaPort();
        return Ok(listings.Select(g => GameListing.From(g, port)));
    }

    /// <summary>
    /// Legacy: get server hosting a specific game.
    /// </summary>
    [HttpPost]
    public IActionResult Post(int gameId)
    {
        var code = new GameCode(gameId);
        var game = _gameManager.Find(code);
        if (game == null)
        {
            return NotFound(new MatchmakerResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        var port = GetDeltaPort();
        return Ok(HostServer.From(IPAddress.Parse(_serverConfig.ResolvePublicIp()), port));
    }

    /// <summary>
    /// Legacy: get server address to host a new game.
    /// </summary>
    [HttpPut]
    public IActionResult Put()
    {
        var port = GetDeltaPort();
        return Ok(HostServer.From(IPAddress.Parse(_serverConfig.ResolvePublicIp()), port));
    }

    [HttpGet("{gameId}")]
    public IActionResult Show([FromRoute] int gameId)
    {
        var code = new GameCode(gameId);
        var game = _gameManager.Find(code);
        if (game == null)
        {
            return NotFound(new FindGameByCodeResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        var port = GetDeltaPort();
        return Ok(new FindGameByCodeResponse(GameListing.From(game, port)));
    }

    /// <summary>
    /// Filtered lobby list (Among Us 16.0.0+).
    /// No support filter conditions on the latest version.
    /// </summary>
    [HttpGet("filtered")]
    public IActionResult ShowFilteredLobbies()
    {
        var port = GetDeltaPort();
        var listings = _gameManager.Games
            .Where(g => g.IsPublic)
            .Select(g => GameListing.From(g, port))
            .ToList();

        return Ok(new
        {
            games = listings,
            metadata = new
            {
                allGamesCount = _gameManager.Games.Count(),
                matchingGamesCount = listings.Count,
            },
        });
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
    ///     Resolves the UDP port the requesting client should connect to.
    ///     In dynamic delta mode every client is handed a unique UDP port in its
    ///     auth response (POST /api/user) and echoes that token back as the
    ///     Authorization bearer header on subsequent requests, so the port is
    ///     read from the token. Falls back to the configured public port.
    /// </summary>
    private ushort GetDeltaPort()
    {
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
                // Malformed or non-base64 token — fall through to the public port.
            }
        }

        return _serverConfig.PublicPort;
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
        public MatchmakerError(DisconnectReason reason) { Reason = reason; }

        [JsonPropertyName("Reason")]
        public required DisconnectReason Reason { get; init; }
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

        public static GameListing From(IGame game, ushort port)
        {
            var platform = game.Host?.Client.PlatformSpecificData;
            return new GameListing
            {
                Ip = ConvertAddressToNumber(game.PublicIp.Address),
                Port = port,
                GameId = game.Code,
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

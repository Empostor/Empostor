using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Server.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Http;

[Route("/api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameManager _gameManager;
    private readonly ListingManager _listingManager;
    private readonly HostServer _hostServer;

    public GamesController(IGameManager gameManager, ListingManager listingManager, IOptions<ServerConfig> serverConfig)
    {
        _gameManager = gameManager;
        _listingManager = listingManager;
        var config = serverConfig.Value;
        _hostServer = HostServer.From(IPAddress.Parse(config.ResolvePublicIp()), config.PublicPort);
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
        return Ok(listings.Select(GameListing.From));
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

        return Ok(HostServer.From(game.PublicIp));
    }

    /// <summary>
    /// Legacy: get server address to host a new game.
    /// </summary>
    [HttpPut]
    public IActionResult Put() => Ok(_hostServer);

    /// <summary>
    /// Get a specific game by code.
    /// </summary>
    [HttpGet("{gameId}")]
    public IActionResult Show([FromRoute] int gameId)
    {
        var code = new GameCode(gameId);
        var game = _gameManager.Find(code);
        if (game == null)
        {
            return NotFound(new FindGameByCodeResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        return Ok(new FindGameByCodeResponse(GameListing.From(game)));
    }

    /// <summary>
    /// Filtered lobby list (Among Us 16.0.0+).
    /// No support filter conditions on the latest version.
    /// </summary>
    [HttpGet("filtered")]
    public IActionResult ShowFilteredLobbies()
    {
        var listings = _gameManager.Games
            .Where(g => g.IsPublic)
            .Select(GameListing.From)
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
            players = g.Players.Select(p => new
            {
                name = p.Client.Name,
                friendCode = p.Client.FriendCode ?? "UnknownFriendCode",
                isHost = p.IsHost,
                platform = p.Client.PlatformSpecificData?.PlatformName ?? "UnknownPlatform",
            }).ToList(),
        }).ToList();

        return Ok(new
        {
            totalGames = games.Count,
            totalPlayers = games.Sum(g => g.playerCount),
            games,
        });
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

        public static GameListing From(IGame game)
        {
            var platform = game.Host?.Client.PlatformSpecificData;
            return new GameListing
            {
                Ip = ConvertAddressToNumber(game.PublicIp.Address),
                Port = (ushort)game.PublicIp.Port,
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

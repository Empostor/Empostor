using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.GameFilters;
using Impostor.Server.Extensions;
using Impostor.Server.Net;
using Impostor.Server.Service.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Http;

/// <summary>
/// This controller has method to get a list of public games, join by game and create new games.
/// </summary>
[Route("/api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameManager _gameManager;
    private readonly ListingManager _listingManager;
    private readonly HostServer _hostServer;
    private readonly ILogger<GamesController> _logger;
    private readonly ServerConfig _serverConfig;
    private readonly AuthCacheService _authCache;
    private readonly GameCodeAuthManager _gameCodeAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="GamesController"/> class.
    /// </summary>
    /// <param name="gameManager">GameManager containing a list of games.</param>
    /// <param name="listingManager">ListingManager responsible for filtering.</param>
    /// <param name="serverConfig">Impostor configuration section containing the public ip address of this server.</param>
    /// <param name="logger">DI injected logger.</param>
    public GamesController(IGameManager gameManager, ListingManager listingManager, IOptions<ServerConfig> serverConfig, ILogger<GamesController> logger, AuthCacheService authCache, GameCodeAuthManager gameCodeAuth)
    {
        _gameManager = gameManager;
        _listingManager = listingManager;
        var config = serverConfig.Value;
        _serverConfig = config;
        _hostServer = HostServer.From(IPAddress.Parse(config.ResolvePublicIp()), config.PublicPort);
        _logger = logger;
        _authCache = authCache;
        _gameCodeAuth = gameCodeAuth;
    }

    /// <summary>
    /// Get a list of active games.
    /// </summary>
    /// <param name="mapId">Maps that are requested.</param>
    /// <param name="lang">Preferred chat language.</param>
    /// <param name="numImpostors">Amount of impostors. 0 is any.</param>
    /// <param name="authorization">Authorization header containing the matchmaking token.</param>
    /// <returns>An array of game listings.</returns>
    [HttpGet]
    public IActionResult Index(int mapId, GameKeywords lang, int numImpostors, [FromHeader] AuthenticationHeaderValue authorization)
    {
        // NOTE: this method is no longer used by Among Us 16.0.0 and is only kept for backwards compatibility
        if (authorization.Scheme != "Bearer" || authorization.Parameter == null)
        {
            return BadRequest();
        }

        var token = JsonSerializer.Deserialize<TokenController.Token>(Convert.FromBase64String(authorization.Parameter));
        if (token == null)
        {
            return BadRequest();
        }

        var clientVersion = new GameVersion(token.Content.ClientVersion);

        var auth = _serverConfig.IsVerificationDisabled ? null : _authCache.FindByToken(token.Hash);
        if (_serverConfig.UseGameCodeMode && auth == null) return Unauthorized();
        var listings = _listingManager.FindListings(HttpContext, mapId, numImpostors, lang, clientVersion);
        return Ok(listings.Select(game => BuildListing(game, auth)));
    }

    /// <summary>
    /// Get the address a certain game is hosted at.
    /// </summary>
    /// <param name="gameId">The id of the game that should be retrieved.</param>
    /// <returns>The server this game is hosted on.</returns>
    [HttpPost]
    public IActionResult Post(int gameId)
    {
        // NOTE: this method is no longer used by Among Us 16.0.0 and is only kept for backwards compatibility
        var code = new GameCode(gameId);
        var auth = TryGetAuthInfo();
        if (_serverConfig.UseGameCodeMode && auth == null) return Unauthorized();

        var game = _gameManager.Find(code);

        // If the game was not found, print an error message.
        if (game == null)
        {
            return NotFound(new MatchmakerResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        if (_serverConfig.UseGameCodeMode && auth != null && !_gameCodeAuth.TryExpectJoin(auth, code))
        {
            return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.Custom, "Authentication failed; please retry.")));
        }

        var port = _serverConfig.UseDeltaPortMode && auth?.Port > 0
            ? (ushort)auth.Port
            : _serverConfig.UseGameCodeMode ? _serverConfig.GameCodePublicPort : (ushort)game.PublicIp.Port;
        return Ok(HostServer.From(game.PublicIp.Address, port));
    }

    /// <summary>
    /// Get the address to host a new game on.
    /// </summary>
    /// <returns>The address of this server.</returns>
    [HttpPut]
    public IActionResult Put()
    {
        if (_serverConfig.IsVerificationDisabled) return Ok(_hostServer);

        var auth = TryGetAuthInfo();
        if (_serverConfig.UseGameCodeMode)
        {
            if (auth == null) return Unauthorized();
            if (!_gameCodeAuth.TryExpectHost(auth))
            {
                return BadRequest(new MatchmakerResponse(new MatchmakerError(DisconnectReason.Custom, "Authentication failed; please retry.")));
            }
        }

        var port = _serverConfig.UseDeltaPortMode && auth?.Port > 0
            ? (ushort)auth.Port
            : _serverConfig.UseGameCodeMode ? _serverConfig.GameCodePublicPort : _serverConfig.PublicPort;
        return Ok(HostServer.From(IPAddress.Parse(_serverConfig.ResolvePublicIp()), port));
    }

    [HttpGet("{gameId}")]
    public IActionResult Show([FromRoute] int gameId)
    {
        var code = new GameCode(gameId);
        var auth = TryGetAuthInfo();

        var game = _gameManager.Find(code);

        // If the game was not found, print an error message.
        if (game == null)
        {
            return NotFound(new FindGameByCodeResponse(new MatchmakerError(DisconnectReason.GameNotFound)));
        }

        if (_serverConfig.UseGameCodeMode && auth == null) return Unauthorized();
        return Ok(new FindGameByCodeResponse(BuildListing(game, auth)));
    }

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

            var auth = TryGetAuthInfo();
            if (_serverConfig.UseGameCodeMode && auth == null) return Unauthorized();
            var filteredGames = _listingManager.FindListingsV2(HttpContext, filtersList);
            var gameListings = filteredGames.Select(game => BuildListing(game, auth)).ToList();

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

    private UserAuthInfo? TryGetAuthInfo()
    {
        if (_serverConfig.IsVerificationDisabled) return null;

        var header = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter)) return null;
        try
        {
            var token = JsonSerializer.Deserialize<TokenController.Token>(Convert.FromBase64String(authorization.Parameter));
            return token == null ? null : _authCache.FindByToken(token.Hash);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private GameListing BuildListing(IGame game, UserAuthInfo? auth)
    {
        if (_serverConfig.IsVerificationDisabled) return GameListing.From(game);

        var code = game.Code;
        var port = _serverConfig.PublicPort;
        if (_serverConfig.UseGameCodeMode)
        {
            code = _gameCodeAuth.IssueAlias(auth!, game.Code, candidate => _gameManager.Find(new GameCode(candidate)) != null);
            port = _serverConfig.GameCodePublicPort;
        }
        else if (_serverConfig.UseDeltaPortMode && auth?.Port > 0)
        {
            port = (ushort)auth.Port;
        }

        return GameListing.From(game, code, port);
    }

    private static uint ConvertAddressToNumber(IPAddress address)
    {
#pragma warning disable CS0618 // Among Us only supports IPv4
        return (uint)address.Address;
#pragma warning restore CS0618
    }

    private class HostServer
    {
        [JsonPropertyName("Ip")]
        public required long Ip { get; init; }

        [JsonPropertyName("Port")]
        public required ushort Port { get; init; }

        public static HostServer From(IPAddress ipAddress, ushort port)
        {
            return new HostServer
            {
                Ip = ConvertAddressToNumber(ipAddress),
                Port = port,
            };
        }

        public static HostServer From(IPEndPoint endPoint)
        {
            return From(endPoint.Address, (ushort)endPoint.Port);
        }
    }

    private class MatchmakerResponse
    {
        [SetsRequiredMembers]
        public MatchmakerResponse(MatchmakerError error)
        {
            Errors = new[] { error };
        }

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
        public FindGameByCodeResponse(MatchmakerError error) => (Errors, Game) = (new[] { error }, null);

        [SetsRequiredMembers]
        public FindGameByCodeResponse(GameListing game) => (Errors, Game) = (null, game);

        [JsonPropertyName("Errors")]
        public required MatchmakerError[]? Errors { get; init; }

        [JsonPropertyName("Game")]
        public required GameListing? Game { get; init; }
    }

    private class GameListing
    {
        [JsonPropertyName("IP")]
        public required uint Ip { get; init; }

        [JsonPropertyName("Port")]
        public required ushort Port { get; init; }

        [JsonPropertyName("GameId")]
        public required int GameId { get; init; }

        [JsonPropertyName("PlayerCount")]
        public required int PlayerCount { get; init; }

        [JsonPropertyName("HostName")]
        public required string HostName { get; init; }

        [JsonPropertyName("TrueHostName")]
        public required string TrueHostName { get; init; }

        [JsonPropertyName("HostPlatformName")]
        public required string HostPlatformName { get; init; }

        [JsonPropertyName("Platform")]
        public required Platforms Platform { get; init; }

        [JsonPropertyName("QuickChat")]
        public required QuickChatModes QuickChat { get; init; }

        [JsonPropertyName("Age")]
        public required int Age { get; init; }

        [JsonPropertyName("MaxPlayers")]
        public required int MaxPlayers { get; init; }

        [JsonPropertyName("NumImpostors")]
        public required int NumImpostors { get; init; }

        [JsonPropertyName("MapId")]
        public required MapTypes MapId { get; init; }

        [JsonPropertyName("Language")]
        public required GameKeywords Language { get; init; }

        [JsonPropertyName("Options")]
        public required string Options { get; init; }

        public static GameListing From(IGame game, GameCode? listingCode = null, ushort? listingPort = null)
        {
            var platform = game.Host?.Client.PlatformSpecificData;

            return new GameListing
            {
                Ip = ConvertAddressToNumber(game.PublicIp.Address),
                Port = listingPort ?? (ushort)game.PublicIp.Port,
                GameId = listingCode ?? game.Code,
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

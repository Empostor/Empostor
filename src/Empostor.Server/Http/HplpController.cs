using System;
using System.Linq;
using Empostor.Api.Config;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Api.Innersloth;
using Empostor.Server.Service.Admin.Reactor;
using Empostor.Server.Service.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Http;

/// <summary>
///     Implements the HPLP (HTTP Public Lobby List Protocol) for Starlight client discovery.
///     Serves <c>GET /x-api/games</c> with active games and region metadata.
/// </summary>
[ApiController]
[Route("/x-api")]
public sealed class HplpController : ControllerBase
{
    private readonly IGameManager _gameManager;
    private readonly HplpStore _hplpStore;
    private readonly ServerConfig _serverConfig;
    private readonly HttpServerConfig _httpConfig;

    public HplpController(
        IGameManager gameManager,
        HplpStore hplpStore,
        IOptions<ServerConfig> serverConfig,
        IOptions<HttpServerConfig> httpConfig)
    {
        _gameManager = gameManager;
        _hplpStore = hplpStore;
        _serverConfig = serverConfig.Value;
        _httpConfig = httpConfig.Value;
    }

    [HttpGet("games")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetGames()
    {
        if (!_hplpStore.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "HPLP is not enabled on this server." });
        }

        var regionUrl = ResolveRegionUrl();
        var regionId = _hplpStore.RegionId;

        var games = _gameManager.Games.Select(game => new
        {
            code = (string)game.Code,
            host_name = game.DisplayName ?? game.Host?.Client.Name ?? "—",
            status = MapStatus(game.GameState),
            player_count = game.PlayerCount,
            max_players = (int)game.Options.MaxPlayers,
            chat_lang = (int)game.Options.Keywords,
            map_id = (int)game.Options.Map,
            region_id = regionId,
            mods = GetMods(game),
        }).ToList();

        return Ok(new
        {
            games,
            regions = new[]
            {
                new
                {
                    id = regionId,
                    name = _hplpStore.RegionName,
                    url = regionUrl,
                },
            },
        });
    }

    private string ResolveRegionUrl()
    {
        if (!string.IsNullOrWhiteSpace(_hplpStore.PublicUrl))
        {
            return _hplpStore.PublicUrl.TrimEnd('/');
        }

        var publicIp = _serverConfig.ResolvePublicIp();
        return $"http://{publicIp}:{_httpConfig.ListenPort}";
    }

    private static string MapStatus(GameStates state)
    {
        return state switch
        {
            GameStates.NotStarted => "Lobby",
            GameStates.Starting => "Lobby",
            GameStates.Started => "Started",
            GameStates.Ended => "Ended",
            GameStates.Destroyed => "Ended",
            _ => "Lobby",
        };
    }

    private static object[] GetMods(IGame game)
    {
        // Try Reactor mods from the host's handshake first.
        var reactorMods = game.Host?.Client.GetReactorMods();
        if (reactorMods != null && reactorMods.Mods.Count > 0)
        {
            return reactorMods.Mods.Select(m => new
            {
                id = m.Id,
                version = m.Version,
                flags = m.RequiredOnAllClients ? 1 : 0,
            }).ToArray<object>();
        }

        return Array.Empty<object>();
    }
}

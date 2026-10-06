using System;
using System.Linq;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Client;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Api.Innersloth;
using Empostor.Api.Net;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Maintenance;

/// <summary>
///     While maintenance mode is on, every player is turned away: the ones already in a lobby are
///     disconnected, and anyone connecting, creating a lobby or trying to join is rejected with the
///     same message.
/// </summary>
public sealed class MaintenanceListener : IEventListener
{
    private readonly ILogger<MaintenanceListener> _logger;
    private readonly MaintenanceStore _store;
    private readonly IGameManager _games;

    public MaintenanceListener(
        ILogger<MaintenanceListener> logger,
        MaintenanceStore store,
        IGameManager games)
    {
        _logger = logger;
        _store = store;
        _games = games;
    }

    /// <summary>Turns maintenance on or off, disconnecting everyone currently in a lobby when on.</summary>
    public async ValueTask SetAsync(bool enabled)
    {
        _store.SetEnabled(enabled);
        await _store.SaveAsync();

        if (!enabled)
        {
            _logger.LogInformation("MaintenanceMode disabled, the server is accepting players again");
            return;
        }

        _logger.LogWarning("MaintenanceMode enabled, disconnecting every player currently in a lobby");
        await KickAllAsync();
    }

    private async ValueTask KickAllAsync()
    {
        foreach (var game in _games.Games.ToList())
        {
            foreach (var player in game.Players.ToList())
            {
                var client = player.Client;
                if (client == null)
                {
                    continue;
                }

                try
                {
                    await client.DisconnectAsync(DisconnectReason.Custom, _store.MessageFor(client.Language));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Maintenance failed to disconnect client {Id}", client.Id);
                }
            }
        }
    }

    [EventListener]
    public async ValueTask OnClientConnected(IClientConnectedEvent e)
    {
        // A client that never reaches a lobby would otherwise sit connected forever.
        if (_store.Enabled)
        {
            await e.Client.DisconnectAsync(DisconnectReason.Custom, _store.MessageFor(e.Client.Language));
        }
    }

    [EventListener]
    public async ValueTask OnGameCreating(IGameCreationEvent e)
    {
        if (_store.Enabled && e.Client != null)
        {
            await e.Client.DisconnectAsync(DisconnectReason.Custom, _store.MessageFor(e.Client.Language));
        }
    }

    [EventListener]
    public void OnPlayerJoining(IGamePlayerJoiningEvent e)
    {
        if (_store.Enabled)
        {
            e.JoinResult = GameJoinResult.CreateCustomError(_store.MessageFor(e.Player.Client.Language));
        }
    }
}

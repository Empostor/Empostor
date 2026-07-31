using System;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Player;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Titles.Service;

public sealed class TitleEventListener : IEventListener
{
    private readonly ILogger<TitleEventListener> _logger;
    private readonly TitleStore _store;

    public TitleEventListener(ILogger<TitleEventListener> logger, TitleStore store)
    {
        _logger = logger;
        _store = store;
    }

    [EventListener]
    public void OnPlayerSpawned(IPlayerSpawnedEvent e)
    {
        // Titles are now applied natively via IClientConnectedEvent in FriendCodeTitleListener.
        // TitleStore-based titles (set via API) are still available via the store.
        var clientId = e.ClientPlayer.Client.Id;
        var title = _store.Get(clientId);
        if (title == null) return;

        // For API-applied titles on already-connected players, we still use SetNameAsync
        // since the client.Name was already set before the API title was added.
        var player = e.ClientPlayer;
        var playerCtrl = e.PlayerControl;

        _ = Task.Run(async () =>
        {
            try
            {
                if (player.Client.Connection == null || !player.Client.Connection.IsConnected)
                    return;

                var displayName = TitleStore.BuildDisplayName(title, player.Client.Name);
                await playerCtrl.SetNameAsync(displayName);

                _logger.LogDebug("[Titles] Applied title [{Title}] to {Name}", title, player.Client.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Titles] Failed to apply title for client {Id}", clientId);
            }
        });
    }

    [EventListener]
    public void OnPlayerDestroyed(IPlayerDestroyedEvent e)
    {
        //_store.Clear(e.ClientPlayer.Client.Id);
    }
}

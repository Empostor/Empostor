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
        var clientId = e.ClientPlayer.Client.Id;
        var title = _store.Get(clientId);
        if (title == null) return;

        // Client.Name was already updated at connection time by FriendCodeTitleListener
        // or at API call time. Now broadcast the title name to all in-game players.
        var playerCtrl = e.PlayerControl;

        _ = Task.Run(async () =>
        {
            try
            {
                if (e.ClientPlayer.Client.Connection == null
                    || !e.ClientPlayer.Client.Connection.IsConnected)
                    return;

                var displayName = TitleStore.BuildDisplayName(title, e.ClientPlayer.Client.Name);
                await playerCtrl.SetNameAsync(displayName);

                // Title applied in-game, clear from store
                _store.Clear(clientId);

                _logger.LogDebug("[Titles] In-game name set [{Title}] for client {Id}",
                    title, clientId);
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
        // Clean up any lingering title
        //_store.Clear(e.ClientPlayer.Client.Id);
    }
}

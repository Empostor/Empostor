using System;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Titles.Service;

public sealed class TitleEventListener : IEventListener
{
    private readonly ILogger<TitleEventListener> _logger;
    private readonly TitleStore _store;

    public TitleEventListener(ILogger<TitleEventListener> logger, TitleStore store)
    {
        _logger = logger;
        _store  = store;
    }

    [EventListener]
    public void OnPlayerSpawned(IPlayerSpawnedEvent e)
    {
        var clientId = e.ClientPlayer.Client.Id;
        var title = _store.Get(clientId);
        if (title == null) return;

        var player = e.ClientPlayer;
        var playerCtrl = e.PlayerControl;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(600));

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

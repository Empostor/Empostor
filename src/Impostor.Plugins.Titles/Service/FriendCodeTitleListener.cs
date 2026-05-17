using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Game.Player;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Titles.Service;

public sealed class FriendCodeTitleListener : IEventListener
{
    private readonly ILogger<FriendCodeTitleListener> _logger;
    private readonly Dictionary<string, string> _map;

    public FriendCodeTitleListener(
        ILogger<FriendCodeTitleListener> logger,
        TitlesConfig config)
    {
        _logger = logger;
        _map = config.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.FriendCode) && !string.IsNullOrWhiteSpace(t.Title))
            .ToDictionary(t => t.FriendCode, t => t.Title, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("[FriendCodeTitles] Loaded {Count} title mapping(s).", _map.Count);
    }

    [EventListener]
    public void OnPlayerReady(IPlayerReadyEvent e)
    {
        var fc = e.ClientPlayer.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;
        if (!_map.TryGetValue(fc, out var title)) return;

        var player = e.ClientPlayer;
        var ctrl = e.PlayerControl;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(600));

                if (player.Client.Connection == null || !player.Client.Connection.IsConnected)
                    return;

                var displayName = TitleStore.BuildDisplayName(title, player.Client.Name);
                await ctrl.SetNameAsync(displayName);

                _logger.LogInformation("[FriendCodeTitles] Applied [{Title}] to {Name} ({FC})",
                    title, player.Client.Name, fc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FriendCodeTitles] Failed to apply title for {FC}", fc);
            }
        });
    }
}

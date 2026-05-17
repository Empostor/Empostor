using System;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Game.Player;
using Impostor.Api.Innersloth;
using Impostor.Api.Languages;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Welcome.Service;

public sealed class WelcomeEventListener : IEventListener
{
    private readonly ILogger<WelcomeEventListener> _logger;
    private readonly LanguageService _lang;

    public WelcomeEventListener(ILogger<WelcomeEventListener> logger, LanguageService lang)
    {
        _logger = logger;
        _lang = lang;
    }

    [EventListener]
    public void OnPlayerSpawned(IPlayerReadyEvent e)
    {
        if (e.Game.GameState != GameStates.NotStarted) return;

        var player = e.ClientPlayer;
        var playerCtrl = e.PlayerControl;

        Task.Run(async () =>
        {
            try
            {
                if (player.Client.Connection == null || !player.Client.Connection.IsConnected)
                    return;

                var message = _lang
                    .Get("welcome.join", player.Client.Language)
                    .Format(
                        player.Client.Name,
                        player.Client.FriendCode ?? "—",
                        e.Game.Code.Code);

                await playerCtrl.SendChatToPlayerAsync(message, playerCtrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Welcome] Failed to send welcome message");
            }
        });
    }
}

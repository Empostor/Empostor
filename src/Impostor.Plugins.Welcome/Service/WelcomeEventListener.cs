using System;
using System.IO;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Game.Player;
using Impostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Welcome.Service;

public sealed class WelcomeEventListener : IEventListener
{
    private const string TextDir = "Messages";
    private const string FallbackFile = "EnglishHelloWord.txt";

    private readonly ILogger<WelcomeEventListener> _logger;

    public WelcomeEventListener(ILogger<WelcomeEventListener> logger)
    {
        _logger = logger;
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

                var baseDir = Path.Combine(Directory.GetCurrentDirectory(), TextDir);
                var filePath = Path.Combine(baseDir, $"{player.Client.Language}HelloWord.txt");

                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(baseDir, FallbackFile);
                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("[Welcome] No HelloWord.txt found for language {Language}, fallback missing too.", player.Client.Language);
                        return;
                    }
                }

                var message = File.ReadAllText(filePath);
                var formattedMessage = string.Format(message, player.Client.Name, e.Game.Code.Code);

                await playerCtrl.SendChatToPlayerAsync(formattedMessage, playerCtrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Welcome] Failed to send welcome message");
            }
        });
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Service;
using Empostor.Api.Events.Game.Player;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Welcome.Service;

public sealed class WelcomeEventListener : IEventListener
{
    private const string TextDir = "Message";
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

                var template = File.ReadAllText(filePath);
                var message = FormatMessage(template, player);

                await playerCtrl.SendChatToPlayerAsync(message, playerCtrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Welcome] Failed to send welcome message");
            }
        });
    }

    private static string FormatMessage(string template, Api.Net.IClientPlayer player)
    {
        var lastConnect = player.Client.ProductUserId != null
            ? PlayerConnectStore.GetLastConnectString(player.Client.ProductUserId)
            : null;

        return template
            .Replace("{Name}", player.Client.Name ?? "Player")
            .Replace("{Room}", player.Game.Code.Code)
            .Replace("{FriendCode}", player.Client.FriendCode ?? "None")
            .Replace("{LastConnect}", lastConnect ?? "First time!");
    }
}

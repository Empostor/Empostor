using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Admin.Chat;

internal sealed class ChatFilterListener : IEventListener
{
    private readonly ILogger<ChatFilterListener> _logger;
    private readonly ChatFilterStore _store;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _playerMessages = new();

    public ChatFilterListener(ILogger<ChatFilterListener> logger, ChatFilterStore store)
    {
        _logger = logger;
        _store = store;
    }

    [EventListener]
    public void OnPlayerChat(IPlayerChatEvent e)
    {
        if (!_store.Enabled)
        {
            return;
        }

        var playerKey = e.ClientPlayer.Client.FriendCode ?? e.ClientPlayer.Client.Name;
        var message = e.Message;

        // Word filter
        var words = _store.BlockedWords;
        if (words.Count > 0)
        {
            var lower = message.ToLowerInvariant();
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }

                if (lower.Contains(word.ToLowerInvariant()))
                {
                    if (_store.BlockMessage)
                    {
                        e.IsCancelled = true;
                        _logger.LogInformation(
                            "[ChatFilter] Blocked message from {Name} ({FC}): {Message}",
                            e.ClientPlayer.Client.Name, playerKey, message);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[ChatFilter] Flagged (not blocked) from {Name} ({FC}): {Message}",
                            e.ClientPlayer.Client.Name, playerKey, message);
                    }

                    return;
                }
            }
        }

        // Spam rate limit
        var threshold = _store.SpamThreshold;
        if (threshold > 0)
        {
            var queue = _playerMessages.GetOrAdd(playerKey, _ => new Queue<DateTime>());
            lock (queue)
            {
                var now = DateTime.UtcNow;
                var window = TimeSpan.FromSeconds(_store.SpamWindowSeconds);
                while (queue.Count > 0 && now - queue.Peek() > window)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= threshold)
                {
                    if (_store.BlockMessage)
                    {
                        e.IsCancelled = true;
                        _logger.LogInformation(
                            "[ChatFilter] Rate-limited {Name} ({FC})", e.ClientPlayer.Client.Name, playerKey);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[ChatFilter] Rate-limit flag (not blocked) for {Name} ({FC})", e.ClientPlayer.Client.Name, playerKey);
                    }

                    return;
                }

                queue.Enqueue(now);
            }
        }
    }
}

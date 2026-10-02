using System;
using System.Collections.Generic;
using System.Linq;

namespace Empostor.Plugin.Chat;

/// <summary>One captured in-game chat line.</summary>
public sealed record ChatEntry(
    DateTime Time,
    string GameCode,
    string PlayerName,
    string? FriendCode,
    int ClientId,
    string Channel,
    string Message,
    bool Blocked);

/// <summary>
///     In-memory ring buffer of chat lines, fed by <see cref="ChatService"/>. Nothing is written to
///     disk — the admin panel is the only reader and the history is meant to be short-lived.
/// </summary>
public sealed class ChatStore
{
    private readonly object _lock = new();
    private readonly LinkedList<ChatEntry> _entries = new();
    private readonly int _capacity;

    public ChatStore()
    {
        var config = ChatConfig.Load();
        _capacity = Math.Max(50, config.MaxBufferedMessages);
    }

    /// <summary>Oldest lines are dropped once the buffer is full.</summary>
    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public void Add(ChatEntry entry)
    {
        lock (_lock)
        {
            _entries.AddLast(entry);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveFirst();
            }
        }
    }

    /// <summary>Newest first, optionally limited to one room.</summary>
    public List<ChatEntry> Recent(string? gameCode, int limit)
    {
        lock (_lock)
        {
            var query = _entries.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(gameCode))
            {
                query = query.Where(x => string.Equals(x.GameCode, gameCode, StringComparison.OrdinalIgnoreCase));
            }

            return query.Reverse().Take(Math.Max(1, limit)).ToList();
        }
    }

    /// <summary>Room codes that currently have buffered lines, so the panel can offer a filter.</summary>
    public List<string> Rooms()
    {
        lock (_lock)
        {
            return _entries
                .Select(x => x.GameCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}

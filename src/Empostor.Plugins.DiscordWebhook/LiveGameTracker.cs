using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Empostor.Api.Games;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

/// <summary>One live Discord message that mirrors a lobby's state.</summary>
public sealed class TrackedLobby
{
    public int GameCode { get; init; }

    public string GameCodeName { get; init; } = string.Empty;

    public string WebhookUrl { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    /// <summary>Live reference so deferred updates can render the current state. Never persisted.</summary>
    [JsonIgnore]
    public IGame? Game { get; set; }
}

/// <summary>
///     Maps live lobbies to the Discord message mirroring them. The mapping is persisted so that
///     messages orphaned by a server restart can be deleted on the next start — lobbies never
///     survive a restart, so every tracked message found at startup is stale by definition.
/// </summary>
public sealed class LiveGameTracker : JsonDataStore<List<TrackedLobby>>
{
    private readonly ConcurrentDictionary<int, TrackedLobby> _tracked = new();

    public LiveGameTracker(ILogger<LiveGameTracker> logger)
        : base(logger)
    {
        Load();
    }

    public void Track(TrackedLobby lobby)
    {
        _tracked[lobby.GameCode] = lobby;
        SaveFireAndForget();
    }

    public bool TryGet(int gameCode, out TrackedLobby lobby)
        => _tracked.TryGetValue(gameCode, out lobby!);

    public bool Untrack(int gameCode, out TrackedLobby lobby)
    {
        var removed = _tracked.TryRemove(gameCode, out var removedLobby);
        lobby = removedLobby!;
        if (removed)
        {
            SaveFireAndForget();
        }

        return removed;
    }

    public IReadOnlyCollection<TrackedLobby> All => _tracked.Values.ToList().AsReadOnly();

    public void Clear()
    {
        _tracked.Clear();
        SaveFireAndForget();
    }

    protected override List<TrackedLobby> GetSnapshot() => _tracked.Values.ToList();

    protected override void ApplySnapshot(List<TrackedLobby> data)
    {
        _tracked.Clear();
        foreach (var lobby in data)
        {
            _tracked[lobby.GameCode] = lobby;
        }
    }
}

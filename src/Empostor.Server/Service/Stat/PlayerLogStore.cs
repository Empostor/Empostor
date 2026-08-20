using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Stat;

public sealed class PlayerLogEntry
{
    public DateTime Time { get; init; }

    public string Type { get; init; } = string.Empty;

    public int? ClientId { get; init; }

    public string? PlayerName { get; init; }

    public string? FriendCode { get; init; }

    public string? GameCode { get; init; }

    public string? Detail { get; init; }
}

public sealed class PlayerLogStore : JsonDataStore<List<PlayerLogEntry>>
{
    private const int MaxEntries = 10000;
    private readonly ConcurrentQueue<PlayerLogEntry> _entries = new();
    private int _count;

    public PlayerLogStore(ILogger<PlayerLogStore> logger)
        : base(logger, legacyPath: "Data/player_logs.json")
    {
        Load();
    }

    public void Add(string type, int? clientId, string? playerName, string? friendCode, string? gameCode, string? detail)
    {
        var entry = new PlayerLogEntry
        {
            Time = DateTime.UtcNow,
            Type = type,
            ClientId = clientId,
            PlayerName = playerName,
            FriendCode = friendCode,
            GameCode = gameCode,
            Detail = detail,
        };

        _entries.Enqueue(entry);

        if (Interlocked.Increment(ref _count) > MaxEntries)
        {
            _entries.TryDequeue(out _);
            Interlocked.Decrement(ref _count);
        }

        SaveFireAndForget();
    }

    public List<PlayerLogEntry> GetAll() => _entries.ToList();

    public List<PlayerLogEntry> GetByClient(int clientId) =>
        _entries.Where(e => e.ClientId == clientId).ToList();

    public List<int> GetLoggedClientIds() =>
        _entries.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).Distinct().ToList();

    public int GetMaxClientId() =>
    _entries.Where(e => e.ClientId.HasValue)
        .Select(e => e.ClientId!.Value)
        .DefaultIfEmpty(0)
        .Max();

    /// <summary>
    ///     Returns the most recent non-empty player name recorded for a client.
    ///     Used by the admin panel so disconnected players still show their
    ///     original name instead of a generic "Disconnected" label.
    /// </summary>
    public string? GetLatestName(int clientId) =>
        _entries.Where(e => e.ClientId == clientId)
            .OrderByDescending(e => e.Time)
            .Select(e => e.PlayerName)
            .FirstOrDefault(n => !string.IsNullOrEmpty(n));

    /// <summary>
    ///     Returns the most recent non-empty friend code recorded for a client.
    /// </summary>
    public string? GetLatestFriendCode(int clientId) =>
        _entries.Where(e => e.ClientId == clientId)
            .OrderByDescending(e => e.Time)
            .Select(e => e.FriendCode)
            .FirstOrDefault(f => !string.IsNullOrEmpty(f));

    /// <summary>
    ///     Removes logged entries and persists the store.
    ///     When <paramref name="olderThan" /> is set, only entries strictly older
    ///     than the cutoff are removed (recent entries are kept); otherwise all
    ///     entries are removed.
    /// </summary>
    public void Clear(DateTime? olderThan = null)
    {
        if (olderThan.HasValue)
        {
            var kept = _entries.Where(e => e.Time >= olderThan.Value).ToList();
            _entries.Clear();
            _count = 0;
            foreach (var entry in kept)
            {
                _entries.Enqueue(entry);
                Interlocked.Increment(ref _count);
            }
        }
        else
        {
            _entries.Clear();
            _count = 0;
        }

        SaveFireAndForget();
    }

    public byte[] ExportJson()
    {
        var json = JsonSerializer.Serialize(_entries.ToList(), JsonOpts);
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] ExportJson(int clientId)
    {
        var list = _entries.Where(e => e.ClientId == clientId).ToList();
        var json = JsonSerializer.Serialize(list, JsonOpts);
        return Encoding.UTF8.GetBytes(json);
    }

    protected override List<PlayerLogEntry> GetSnapshot() => _entries.ToList();

    protected override void ApplySnapshot(List<PlayerLogEntry> data)
    {
        _entries.Clear();
        _count = 0;
        foreach (var entry in data)
        {
            _entries.Enqueue(entry);
            Interlocked.Increment(ref _count);
        }

        while (_count > MaxEntries)
        {
            _entries.TryDequeue(out _);
            Interlocked.Decrement(ref _count);
        }
    }
}

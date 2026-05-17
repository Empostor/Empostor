using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Impostor.Server.Service.Stat;

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

public sealed class PlayerLogStore
{
    private const int MaxEntries = 10000;
    private readonly ConcurrentQueue<PlayerLogEntry> _entries = new();
    private int _count;

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
    }

    public List<PlayerLogEntry> GetAll() => _entries.ToList();

    public List<PlayerLogEntry> GetByClient(int clientId) =>
        _entries.Where(e => e.ClientId == clientId).ToList();

    public List<int> GetLoggedClientIds() =>
        _entries.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).Distinct().ToList();

    public byte[] ExportJson()
    {
        var json = JsonSerializer.Serialize(_entries.ToList(), new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] ExportJson(int clientId)
    {
        var list = _entries.Where(e => e.ClientId == clientId).ToList();
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }
}

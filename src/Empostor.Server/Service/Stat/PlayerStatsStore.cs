using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Stat;

public sealed class PlayerStatsEntry
{
    public string FriendCode { get; init; } = string.Empty;

    public string? LastKnownName { get; set; }

    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int ImpostorWins { get; set; }

    public int Kills { get; set; }

    public int Deaths { get; set; }

    public int TasksCompleted { get; set; }

    public int TimesExiled { get; set; }

    public DateTime FirstSeen { get; init; } = DateTime.UtcNow;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

public sealed class PlayerStatsStore : JsonDataStore<List<PlayerStatsEntry>>
{
    private ConcurrentDictionary<string, PlayerStatsEntry> _stats = new(StringComparer.OrdinalIgnoreCase);

    public PlayerStatsStore(ILogger<PlayerStatsStore> logger)
        : base(logger, legacyPath: "Data/player_stats.json")
    {
        Load();
    }

    public PlayerStatsEntry GetOrCreate(string friendCode, string? name = null)
    {
        var fc = Normalize(friendCode);
        var entry = _stats.GetOrAdd(fc, _ => new PlayerStatsEntry { FriendCode = fc, FirstSeen = DateTime.UtcNow });
        if (name != null)
        {
            entry.LastKnownName = name;
        }

        entry.LastSeen = DateTime.UtcNow;
        return entry;
    }

    public PlayerStatsEntry? GetByFriendCode(string friendCode)
        => _stats.TryGetValue(Normalize(friendCode), out var e) ? e : null;

    public void RecordKill(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.Kills++;
        SaveFireAndForget();
    }

    public void RecordDeath(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.Deaths++;
        SaveFireAndForget();
    }

    public void RecordTaskCompleted(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.TasksCompleted++;
        SaveFireAndForget();
    }

    public void RecordExile(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.TimesExiled++;
        SaveFireAndForget();
    }

    public void RecordGameEnd(string friendCode, string? name, bool isCrewmateWin, bool wasImpostor)
    {
        var e = GetOrCreate(friendCode, name);
        e.GamesPlayed++;
        if (wasImpostor)
        {
            if (!isCrewmateWin)
            {
                e.ImpostorWins++;
            }
        }
        else
        {
            if (isCrewmateWin)
            {
                e.Wins++;
            }
            else
            {
                e.Losses++;
            }
        }

        SaveFireAndForget();
    }

    public List<PlayerStatsEntry> GetAll()
        => _stats.Values.OrderByDescending(s => s.GamesPlayed).ToList();

    public void ClearAll()
    {
        _stats.Clear();
        SaveFireAndForget();
    }

    protected override List<PlayerStatsEntry> GetSnapshot() => _stats.Values.ToList();

    protected override void ApplySnapshot(List<PlayerStatsEntry> data)
    {
        _stats = new ConcurrentDictionary<string, PlayerStatsEntry>(
            data.ToDictionary(e => e.FriendCode, StringComparer.OrdinalIgnoreCase));
    }

    private static string Normalize(string friendCode)
        => (friendCode ?? string.Empty).Trim();
}

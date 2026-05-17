using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Stat;

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

public sealed class PlayerStatsStore : IDisposable
{
    private static readonly string StatsFile =
        Path.Combine(Directory.GetCurrentDirectory(), "player_stats.json");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    private readonly ILogger<PlayerStatsStore> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private ConcurrentDictionary<string, PlayerStatsEntry> _stats = new(StringComparer.OrdinalIgnoreCase);

    public PlayerStatsStore(ILogger<PlayerStatsStore> logger)
    {
        _logger = logger;
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
        SaveAsync();
    }

    public void RecordDeath(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.Deaths++;
        SaveAsync();
    }

    public void RecordTaskCompleted(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.TasksCompleted++;
        SaveAsync();
    }

    public void RecordExile(string friendCode)
    {
        var e = GetOrCreate(friendCode);
        e.TimesExiled++;
        SaveAsync();
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

        SaveAsync();
    }

    public List<PlayerStatsEntry> GetAll()
        => _stats.Values.OrderByDescending(s => s.GamesPlayed).ToList();

    public void ClearAll()
    {
        _stats.Clear();
        SaveAsync();
    }

    private void SaveAsync()
    {
        _ = Task.Run(async () =>
        {
            if (!await _saveLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                var list = _stats.Values.ToList();
                await File.WriteAllTextAsync(StatsFile, JsonSerializer.Serialize(list, JsonOpts));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[PlayerStats] Failed to save stats"); }
            finally { _saveLock.Release(); }
        });
    }

    private void Load()
    {
        if (!File.Exists(StatsFile))
        {
            _logger.LogDebug("[PlayerStats] No stats file found, starting fresh");
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<PlayerStatsEntry>>(File.ReadAllText(StatsFile));
            if (list != null)
            {
                _stats = new ConcurrentDictionary<string, PlayerStatsEntry>(
                    list.ToDictionary(e => e.FriendCode, StringComparer.OrdinalIgnoreCase));
                _logger.LogInformation("[PlayerStats] Loaded {Count} player stats", _stats.Count);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "[PlayerStats] Failed to load stats file"); }
    }

    private static string Normalize(string friendCode)
        => (friendCode ?? string.Empty).Trim();

    public void Dispose() => _saveLock.Dispose();
}

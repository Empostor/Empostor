using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empostor.Api.Plugins;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.PlayerStats;

public sealed class PlayerStatsStore : JsonDataStore<List<PlayerStatsEntry>>
{
    private const string ConfigFile = "[Empostor.Plugins.PlayerStats]Config.json";

    private ConcurrentDictionary<string, PlayerStatsEntry> _stats = new(StringComparer.OrdinalIgnoreCase);

    public PlayerStatsStore(ILogger<PlayerStatsStore> logger)
        : base(logger, legacyPath: "Data/player_stats.json")
    {
        Load();
        var cfg = PluginConfigLoader.Load<PlayerStatsConfig>(ConfigPath());
        Enabled = cfg.Enabled;
    }

    public bool Enabled { get; private set; }

    public void SetEnabled(bool value)
    {
        Enabled = value;
        var path = ConfigPath();
        var cfg = PluginConfigLoader.Load<PlayerStatsConfig>(path);
        cfg.Enabled = value;
        PluginConfigLoader.Save(path, cfg);
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

    private static string ConfigPath() => Path.Combine(Directory.GetCurrentDirectory(), ConfigFile);
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Innersloth;

namespace Empostor.Plugins.MapVote;

public sealed class MapVoteService
{
    private static readonly MapTypes[] AllMaps =
    {
        MapTypes.Skeld, MapTypes.MiraHQ, MapTypes.Polus, MapTypes.Airship, MapTypes.Fungle
    };

    private static readonly Dictionary<string, MapTypes> MapAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skeld"] = MapTypes.Skeld,
        ["the skeld"] = MapTypes.Skeld,
        ["mira"] = MapTypes.MiraHQ,
        ["mirahq"] = MapTypes.MiraHQ,
        ["mira hq"] = MapTypes.MiraHQ,
        ["polus"] = MapTypes.Polus,
        ["airship"] = MapTypes.Airship,
        ["fungle"] = MapTypes.Fungle,
        // Chinese aliases
        ["骷髅"] = MapTypes.Skeld,
        ["米拉"] = MapTypes.MiraHQ,
        ["波鲁斯"] = MapTypes.Polus,
        ["飞船"] = MapTypes.Airship,
        ["真菌"] = MapTypes.Fungle,
    };

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MapTypes>> _votes = new();
    private readonly ConcurrentDictionary<string, bool> _enabled = new();
    private readonly ConcurrentDictionary<string, bool> _sessionActive = new();

    public bool IsEnabled(string gameCode) => _enabled.GetValueOrDefault(gameCode, true);

    public void SetEnabled(string gameCode, bool enabled) => _enabled[gameCode] = enabled;

    public bool IsSessionActive(string gameCode) => _sessionActive.GetValueOrDefault(gameCode, false);

    public void StartSession(string gameCode) => _sessionActive[gameCode] = true;

    public void StopSession(string gameCode)
    {
        _sessionActive.TryRemove(gameCode, out _);
        _votes.TryRemove(gameCode, out _);
    }

    public bool TryParseMap(string input, out MapTypes map)
    {
        if (MapAliases.TryGetValue(input.Trim(), out map))
            return true;

        if (int.TryParse(input.Trim(), out var id) && Enum.IsDefined(typeof(MapTypes), (byte)id))
        {
            map = (MapTypes)(byte)id;
            if (map != MapTypes.Dleks)
                return true;
        }

        return false;
    }

    public static string MapDisplayName(MapTypes map) => map switch
    {
        MapTypes.Skeld => "The Skeld",
        MapTypes.MiraHQ => "Mira HQ",
        MapTypes.Polus => "Polus",
        MapTypes.Airship => "Airship",
        MapTypes.Fungle => "Fungle",
        _ => map.ToString(),
    };

    public void CastVote(string gameCode, string playerName, MapTypes map)
    {
        var gameVotes = _votes.GetOrAdd(gameCode, _ => new ConcurrentDictionary<string, MapTypes>());
        gameVotes[playerName] = map;
    }

    public MapTypes? GetPlayerVote(string gameCode, string playerName)
    {
        if (_votes.TryGetValue(gameCode, out var gameVotes) && gameVotes.TryGetValue(playerName, out var map))
            return map;
        return null;
    }

    public IReadOnlyDictionary<MapTypes, int> TallyVotes(string gameCode)
    {
        if (!_votes.TryGetValue(gameCode, out var gameVotes) || gameVotes.IsEmpty)
            return new Dictionary<MapTypes, int>();

        return gameVotes.Values
            .GroupBy(m => m)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public int VoterCount(string gameCode)
    {
        if (!_votes.TryGetValue(gameCode, out var gameVotes))
            return 0;
        return gameVotes.Count;
    }

    public MapTypes GetWinner(string gameCode)
    {
        var tally = TallyVotes(gameCode);
        if (tally.Count == 0)
        {
            var rng = new Random();
            return AllMaps[rng.Next(AllMaps.Length)];
        }

        var maxVotes = tally.Values.Max();
        var winners = tally.Where(kv => kv.Value == maxVotes).Select(kv => kv.Key).ToList();

        if (winners.Count == 1)
            return winners[0];

        var rng2 = new Random();
        return winners[rng2.Next(winners.Count)];
    }

    public void ResetVotes(string gameCode)
    {
        _votes.TryRemove(gameCode, out _);
    }

    internal void Remove(string gameCode)
    {
        _votes.TryRemove(gameCode, out _);
        _enabled.TryRemove(gameCode, out _);
        _sessionActive.TryRemove(gameCode, out _);
    }
}

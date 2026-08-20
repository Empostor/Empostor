using System;

namespace Empostor.Plugins.PlayerStats;

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

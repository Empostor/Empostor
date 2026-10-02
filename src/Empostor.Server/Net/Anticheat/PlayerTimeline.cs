namespace Empostor.Server.Net.Anticheat
{
    internal sealed class PlayerTimeline
    {
        /// <summary>Gets or sets the moment the player's control was spawned.</summary>
        public double SpawnedAt { get; set; } = -1;

        /// <summary>Gets or sets the moment of the last completed kill.</summary>
        public double LastKillAt { get; set; } = -1;

        /// <summary>Gets or sets the victim of the last completed kill.</summary>
        public byte LastKillTarget { get; set; } = byte.MaxValue;

        /// <summary>Gets or sets how many times in a row this player killed an already dead victim.</summary>
        public int DeadTargetKills { get; set; }

        /// <summary>Gets or sets the moment of the last sabotage this player triggered.</summary>
        public double LastSabotageAt { get; set; } = -1;

        /// <summary>Gets or sets the system of the last sabotage this player triggered.</summary>
        public int LastSabotageSystem { get; set; } = -1;

        /// <summary>Gets or sets the moment of the vent in of a possible vent kick exploit.</summary>
        public double VentExploitPendingAt { get; set; } = -1;

        public void NoteKill(byte victimId, double now)
        {
            LastKillAt = now;
            LastKillTarget = victimId;
        }

        public void ResetRound()
        {
            SpawnedAt = -1;
            LastKillAt = -1;
            LastKillTarget = byte.MaxValue;
            DeadTargetKills = 0;
            LastSabotageAt = -1;
            LastSabotageSystem = -1;
            VentExploitPendingAt = -1;
        }
    }
}

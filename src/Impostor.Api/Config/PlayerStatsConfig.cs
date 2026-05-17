namespace Impostor.Api.Config
{
    public class PlayerStatsConfig
    {
        public const string Section = "PlayerStats";

        public bool Enabled { get; set; } = false;

        public bool PersistToFile { get; set; } = true;
    }
}

using System.Collections.Generic;

namespace Impostor.Api.Config
{
    public class ChatFilterConfig
    {
        public const string Section = "ChatFilter";

        public bool Enabled { get; set; } = false;

        public List<string> BlockedWords { get; set; } = new();

        public bool BlockMessage { get; set; } = true;

        public int SpamThreshold { get; set; } = 5;

        public int SpamWindowSeconds { get; set; } = 10;
    }
}

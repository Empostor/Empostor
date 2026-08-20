using System.Collections.Generic;

namespace Empostor.Plugins.ChatFilter;

public sealed class ChatFilterConfig
{
    public bool Enabled { get; set; }

    public List<string> BlockedWords { get; set; } = new();

    public bool BlockMessage { get; set; } = true;

    public int SpamThreshold { get; set; } = 5;

    public int SpamWindowSeconds { get; set; } = 10;
}

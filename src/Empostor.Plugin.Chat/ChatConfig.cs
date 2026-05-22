namespace Empostor.Plugin.Chat;

public sealed class ChatConfig
{
    public int PlayerMaxMessageLength { get; set; } = 300;

    public int HostMaxMessageLength { get; set; } = 1200;

    public string TooLongMessage { get; set; } = "[SERVER] Couldn't send your message, it was too long.";
}

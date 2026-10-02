using System;
using System.IO;
using System.Text.Json;

namespace Empostor.Plugin.Chat;

public sealed class ChatConfig
{
    public int PlayerMaxMessageLength { get; set; } = 300;

    public int HostMaxMessageLength { get; set; } = 1200;

    public string TooLongMessage { get; set; } = "[SERVER] Couldn't send your message, it was too long.";

    /// <summary>Keep a rolling copy of in-game chat for the admin panel's Chat Monitor page.</summary>
    public bool CaptureMessages { get; set; } = true;

    /// <summary>How many chat lines to keep in memory. Oldest lines are dropped first.</summary>
    public int MaxBufferedMessages { get; set; } = 500;

    /// <summary>How many lines the Chat Monitor renders per refresh.</summary>
    public int MaxVisibleMessages { get; set; } = 60;

    /// <summary>
    ///     Reads <c>boot_chat.json</c> next to the server executable, writing the file with defaults
    ///     when it is missing. Shared by <see cref="ChatService"/> and <see cref="ChatStore"/>.
    /// </summary>
    public static ChatConfig Load()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "boot_chat.json");
        if (!File.Exists(path))
        {
            try
            {
                File.WriteAllText(
                    path,
                    JsonSerializer.Serialize(new { Chat = new ChatConfig() }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException)
            {
                // Read-only volume: fall through to the in-memory defaults.
            }

            return new ChatConfig();
        }

        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Chat", out var chatEl))
            {
                var cfg = JsonSerializer.Deserialize<ChatConfig>(chatEl.GetRawText());
                if (cfg != null)
                {
                    return cfg;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
        }

        return new ChatConfig();
    }
}

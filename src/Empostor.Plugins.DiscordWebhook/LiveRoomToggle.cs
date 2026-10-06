using System.Collections.Concurrent;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class LiveRoomToggle
{
    private readonly ConcurrentDictionary<int, bool> _overrides = new();

    public bool IsMuted(int gameCode) => _overrides.TryGetValue(gameCode, out var enabled) && !enabled;

    public void Set(int gameCode, bool enabled) => _overrides[gameCode] = enabled;

    public void Clear(int gameCode) => _overrides.TryRemove(gameCode, out _);
}

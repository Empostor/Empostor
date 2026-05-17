using System.Collections.Concurrent;

namespace Impostor.Plugins.Titles;

public sealed class TitleStore
{
    private readonly ConcurrentDictionary<int, string> _titles = new();

    public void Set(int clientId, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _titles.TryRemove(clientId, out _);
        }
        else
        {
            _titles[clientId] = title.Trim();
        }
    }

    public string? Get(int clientId)
        => _titles.TryGetValue(clientId, out var t) ? t : null;

    public void Clear(int clientId) => _titles.TryRemove(clientId, out _);

    public static string BuildDisplayName(string title, string playerName)
        => $"[{title}] {playerName}";
}

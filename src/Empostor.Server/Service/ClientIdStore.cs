using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service;

/// <summary>
///     Persists the last-assigned client ID so that client IDs are never
///     reused across server restarts. Client IDs are used as the stable
///     identity for persisted per-player data (e.g. the player log). Without
///     persistence the ID counter resets to 1 on every restart, which makes
///     old and new log entries that share the same ID belong to different
///     players.
/// </summary>
public sealed class ClientIdStore : JsonDataStore<long>
{
    private long _lastId;

    public ClientIdStore(ILogger<ClientIdStore> logger)
        : base(logger)
    {
        Load();
    }

    public void InitializeWithMaxId(long maxId)
    {
        if (maxId > _lastId)
        {
            _lastId = maxId;
            SaveFireAndForget();
        }
    }

    public long GetLastId() => _lastId;

    public void SetLastId(long id)
    {
        _lastId = id;
        SaveFireAndForget();
    }

    protected override long GetSnapshot() => _lastId;

    protected override void ApplySnapshot(long data) => _lastId = data;
}

using Empostor.Api.Service;
using Empostor.Server.Service.Stat;
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

    public ClientIdStore(ILogger<ClientIdStore> logger, PlayerLogStore playerLogs)
        : base(logger)
    {
        Load();

        _lastId = System.Math.Max(_lastId, playerLogs.GetMaxClientId());
        if (_lastId > 0)
        {
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

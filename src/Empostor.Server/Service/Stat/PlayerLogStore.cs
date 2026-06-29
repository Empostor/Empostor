using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Stat;

public sealed class PlayerLogEntry
{
    public DateTime Time { get; init; }

    public string Type { get; init; } = string.Empty;

    public int? ClientId { get; init; }

    public string? PlayerName { get; init; }

    public string? FriendCode { get; init; }

    public string? GameCode { get; init; }

    public string? Detail { get; init; }
}

public sealed class PlayerLogStore : IDisposable
{
    private static readonly string DataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
    private static readonly string LogsFile = Path.Combine(DataDir, "player_logs.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private const int MaxEntries = 10000;
    private readonly ILogger<PlayerLogStore> _logger;
    private readonly ConcurrentQueue<PlayerLogEntry> _entries = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private int _count;

    public PlayerLogStore(ILogger<PlayerLogStore> logger)
    {
        _logger = logger;
        Load();
    }

    public void Add(string type, int? clientId, string? playerName, string? friendCode, string? gameCode, string? detail)
    {
        var entry = new PlayerLogEntry
        {
            Time = DateTime.UtcNow,
            Type = type,
            ClientId = clientId,
            PlayerName = playerName,
            FriendCode = friendCode,
            GameCode = gameCode,
            Detail = detail,
        };

        _entries.Enqueue(entry);

        if (Interlocked.Increment(ref _count) > MaxEntries)
        {
            _entries.TryDequeue(out _);
            Interlocked.Decrement(ref _count);
        }

        SaveAsync();
    }

    public List<PlayerLogEntry> GetAll() => _entries.ToList();

    public List<PlayerLogEntry> GetByClient(int clientId) =>
        _entries.Where(e => e.ClientId == clientId).ToList();

    public List<int> GetLoggedClientIds() =>
        _entries.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).Distinct().ToList();

    public byte[] ExportJson()
    {
        var json = JsonSerializer.Serialize(_entries.ToList(), JsonOpts);
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] ExportJson(int clientId)
    {
        var list = _entries.Where(e => e.ClientId == clientId).ToList();
        var json = JsonSerializer.Serialize(list, JsonOpts);
        return Encoding.UTF8.GetBytes(json);
    }

    private void SaveAsync()
    {
        _ = Task.Run(async () =>
        {
            if (!await _saveLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(DataDir);
                var list = _entries.ToList();
                await File.WriteAllTextAsync(LogsFile, JsonSerializer.Serialize(list, JsonOpts));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlayerLogFailed to save logs");
            }
            finally
            {
                _saveLock.Release();
            }
        });
    }

    private void Load()
    {
        if (!File.Exists(LogsFile))
        {
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<PlayerLogEntry>>(File.ReadAllText(LogsFile));
            if (list != null)
            {
                foreach (var entry in list)
                {
                    _entries.Enqueue(entry);
                    Interlocked.Increment(ref _count);
                }

                while (_count > MaxEntries)
                {
                    _entries.TryDequeue(out _);
                    Interlocked.Decrement(ref _count);
                }

                _logger.LogInformation("PlayerLogLoaded {Count} log(s)", _count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlayerLogFailed to load logs file");
        }
    }

    public void Dispose() => _saveLock.Dispose();
}

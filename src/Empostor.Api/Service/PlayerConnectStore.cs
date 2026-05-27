using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Empostor.Api.Service;

public sealed class PlayerConnectStore : IDisposable
{
    private static PlayerConnectStore? _instance;

    private readonly ConcurrentDictionary<string, DateTime> _lastConnect;
    private readonly Timer _saveTimer;
    private readonly ILogger<PlayerConnectStore> _logger;
    private bool _dirty;

    public PlayerConnectStore(ILogger<PlayerConnectStore> logger)
    {
        _logger = logger;
        _lastConnect = Load();
        _saveTimer = new Timer(_ => SaveIfDirty(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _instance = this;
    }

    public void RecordDisconnect(string productUserId)
    {
        if (string.IsNullOrEmpty(productUserId))
        {
            return;
        }

        _lastConnect[productUserId] = DateTime.UtcNow;
        _dirty = true;
    }

    public DateTime? GetLastConnectTime(string productUserId)
    {
        return _lastConnect.TryGetValue(productUserId, out var time) ? time : null;
    }

    public static string? GetLastConnectString(string? productUserId)
    {
        if (string.IsNullOrEmpty(productUserId) || _instance == null)
        {
            return null;
        }

        var time = _instance.GetLastConnectTime(productUserId);
        if (time == null)
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(time.Value, TimeZoneInfo.Local);
        return local.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static ConcurrentDictionary<string, DateTime> Load()
    {
        try
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            var path = Path.Combine(dir, "PlayerConnect.json");
            if (!File.Exists(path))
            {
                return new ConcurrentDictionary<string, DateTime>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, DateTime>>(json)
                   ?? new ConcurrentDictionary<string, DateTime>();
        }
        catch
        {
            return new ConcurrentDictionary<string, DateTime>();
        }
    }

    private void SaveIfDirty()
    {
        if (!_dirty)
        {
            return;
        }

        try
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "PlayerConnect.json");
            var json = JsonSerializer.Serialize(_lastConnect);
            File.WriteAllText(path, json);
            _dirty = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlayerConnect] Failed to save player connect data");
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        SaveIfDirty();
        _instance = null;
    }
}

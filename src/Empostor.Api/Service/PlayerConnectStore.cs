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
    private readonly string _filePath;
    private bool _dirty;

    public PlayerConnectStore(ILogger<PlayerConnectStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PlayerConnectData.json");
        _lastConnect = Load();
        _saveTimer = new Timer(_ => SaveIfDirty(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _instance = this;

        // One-time migration from old path
        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PlayerConnect.json");
        if (File.Exists(legacyPath) && !File.Exists(_filePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(legacyPath, _filePath);
                logger.LogInformation("PlayerConnectStore migrated from {Legacy} to {New}", legacyPath, _filePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PlayerConnectStore failed to migrate {Legacy}", legacyPath);
            }
        }
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

    private ConcurrentDictionary<string, DateTime> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ConcurrentDictionary<string, DateTime>();
            }

            var json = File.ReadAllText(_filePath);
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
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_lastConnect);
            File.WriteAllText(_filePath, json);
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
        GC.SuppressFinalize(this);
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Message;

public sealed class MessageStore
{
    private readonly ConcurrentDictionary<string, List<PendingMessage>> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MessageStore> _logger;
    private readonly string _filePath;
    private readonly object _saveLock = new();

    public MessageStore(ILogger<MessageStore> logger, MessageConfig config)
    {
        _logger = logger;
        _filePath = Path.Combine(Directory.GetCurrentDirectory(), "config", "messages.json");
        Load();
    }

    public int Count(string targetFc)
    {
        if (_messages.TryGetValue(targetFc, out var list))
            return list.Count;
        return 0;
    }

    public void Add(PendingMessage msg)
    {
        var list = _messages.GetOrAdd(msg.TargetFc, _ => new List<PendingMessage>());
        lock (list)
        {
            list.Add(msg);
        }
        Save();
    }

    public IReadOnlyList<PendingMessage> TakeAll(string targetFc)
    {
        if (!_messages.TryGetValue(targetFc, out var list) || list.Count == 0)
            return Array.Empty<PendingMessage>();

        List<PendingMessage> taken;
        lock (list)
        {
            taken = list.ToList();
            list.Clear();
        }

        _messages.TryRemove(targetFc, out _);
        Save();
        return taken;
    }

    private void Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<PendingMessage>>>(json);
            if (data == null) return;

            foreach (var (fc, msgs) in data)
            {
                if (msgs.Count > 0)
                    _messages[fc] = msgs;
            }

            var total = _messages.Values.Sum(v => v.Count);
            _logger.LogInformation("[MessageStore] Loaded {Count} pending messages.", total);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MessageStore] Failed to load messages.");
        }
    }

    private void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new Dictionary<string, List<PendingMessage>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (fc, list) in _messages)
                {
                    List<PendingMessage> copy;
                    lock (list)
                    {
                        copy = list.ToList();
                    }

                    if (copy.Count > 0)
                        data[fc] = copy;
                }

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MessageStore] Failed to save messages.");
            }
        }
    }
}

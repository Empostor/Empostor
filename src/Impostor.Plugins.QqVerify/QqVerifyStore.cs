using System;
using System.Collections.Generic;
using Impostor.Api.Service.Admin.Verify;

namespace Impostor.Plugins.QqVerify;

public sealed class QqVerifyStore : IVerifyStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, PendingEntry> _pending = new();
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(10);
    private readonly string _botSecret;

    public QqVerifyStore(QqVerifyConfig config)
    {
        _botSecret = config.BotSecret;
    }

    public void AddPending(string friendCode, string qqNumber)
    {
        var key = Normalize(friendCode);
        lock (_lock)
        {
            CleanExpiredLocked();
            _pending[key] = new PendingEntry(qqNumber, DateTime.UtcNow);
        }
    }

    public bool ValidateSecret(string secret)
    {
        return !string.IsNullOrWhiteSpace(secret) && secret == _botSecret;
    }

    public bool TryConfirm(string friendCode, string qqNumber)
    {
        var key = Normalize(friendCode);
        lock (_lock)
        {
            CleanExpiredLocked();
            if (_pending.TryGetValue(key, out var entry) && entry.QqNumber == qqNumber)
            {
                _pending.Remove(key);
                return true;
            }

            return false;
        }
    }

    private void CleanExpiredLocked()
    {
        var cutoff = DateTime.UtcNow - Expiry;
        var expired = new List<string>();
        foreach (var (k, v) in _pending)
        {
            if (v.Timestamp < cutoff)
            {
                expired.Add(k);
            }
        }

        foreach (var k in expired)
        {
            _pending.Remove(k);
        }
    }

    private static string Normalize(string fc)
    {
        return (fc ?? string.Empty).Trim().ToUpperInvariant();
    }

    private sealed record PendingEntry(string QqNumber, DateTime Timestamp);
}

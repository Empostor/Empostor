using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Admin.Ban
{
    public sealed class BanStore : IDisposable
    {
        private static readonly string BanFile =
            Path.Combine(Directory.GetCurrentDirectory(), "bans.json");

        private static readonly JsonSerializerOptions JsonOpts =
            new() { WriteIndented = true };

        private readonly ILogger<BanStore> _logger;
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private ConcurrentDictionary<string, BanEntry> _ips = new();
        private ConcurrentDictionary<string, BanEntry> _friendCodes = new();

        public BanStore(ILogger<BanStore> logger)
        {
            _logger = logger;
            Load();
        }

        public bool IsIpBanned(IPAddress ip) => _ips.ContainsKey(Normalize(ip));

        public bool IsFriendCodeBanned(string? fc) => fc != null && _friendCodes.ContainsKey(fc);

        public BanEntry BanIp(IPAddress ip, string reason)
        {
            var key = Normalize(ip);
            var entry = new BanEntry { Value = key, Reason = reason, BannedAt = DateTime.UtcNow };
            _ips[key] = entry;
            SaveAsync();
            return entry;
        }

        public bool UnbanIp(string key)
        {
            var r = _ips.TryRemove(key, out _);
            if (r)
            {
                SaveAsync();
            }

            return r;
        }

        public BanEntry BanFriendCode(string fc, string reason)
        {
            var entry = new BanEntry { Value = fc, Reason = reason, BannedAt = DateTime.UtcNow };
            _friendCodes[fc] = entry;
            SaveAsync();
            return entry;
        }

        public bool UnbanFriendCode(string key)
        {
            var r = _friendCodes.TryRemove(key, out _);
            if (r)
            {
                SaveAsync();
            }

            return r;
        }

        public IReadOnlyList<BanEntry> AllIpBans()
            => _ips.Values.OrderByDescending(b => b.BannedAt).ToList();

        public IReadOnlyList<BanEntry> AllFriendCodeBans()
            => _friendCodes.Values.OrderByDescending(b => b.BannedAt).ToList();

        public (int IpCount, int FcCount) Stats() => (_ips.Count, _friendCodes.Count);

        private void SaveAsync()
        {
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                if (!await _saveLock.WaitAsync(0))
                {
                    return;
                }

                try
                {
                    var data = new BanData { Ips = new(_ips), FriendCodes = new(_friendCodes) };
                    await File.WriteAllTextAsync(BanFile, JsonSerializer.Serialize(data, JsonOpts));
                }
                catch (Exception ex) { _logger.LogWarning(ex, "[BanStore] Failed to save bans.json"); }
                finally { _saveLock.Release(); }
            });
        }

        private void Load()
        {
            if (!File.Exists(BanFile))
            {
                return;
            }

            try
            {
                var data = JsonSerializer.Deserialize<BanData>(File.ReadAllText(BanFile));
                if (data != null)
                {
                    _ips = new(data.Ips ?? new());
                    _friendCodes = new(data.FriendCodes ?? new());
                    _logger.LogInformation("[BanStore] Loaded {I} IP bans + {F} FC bans", _ips.Count, _friendCodes.Count);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[BanStore] Failed to load bans.json"); }
        }

        private static string Normalize(IPAddress ip)
            => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

        public void Dispose() => _saveLock.Dispose();

        private sealed class BanData
        {
            [JsonPropertyName("ips")]
            public Dictionary<string, BanEntry>? Ips { get; set; }

            [JsonPropertyName("friendCodes")]
            public Dictionary<string, BanEntry>? FriendCodes { get; set; }
        }
    }

    public sealed class BanEntry
    {
        [JsonPropertyName("value")]
        public required string Value { get; init; }

        [JsonPropertyName("reason")]
        public required string Reason { get; init; }

        [JsonPropertyName("bannedAt")]
        public required DateTime BannedAt { get; init; }
    }
}

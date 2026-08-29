using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Shared
{
    public sealed class BanStore : JsonDataStore<BanStore.BanData>
    {
        private ConcurrentDictionary<string, BanEntry> _ips = new();
        private ConcurrentDictionary<string, BanEntry> _friendCodes = new();

        public BanStore(ILogger<BanStore> logger)
            : base(logger, legacyPath: "bans.json")
        {
            Load();
        }

        public bool IsIpBanned(IPAddress ip) => GetIpBan(ip) != null;

        public bool IsFriendCodeBanned(string? fc) => GetFriendCodeBan(fc) != null;

        public BanEntry? GetIpBan(IPAddress ip)
        {
            var key = Normalize(ip);
            if (!_ips.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (!entry.IsExpired)
            {
                return entry;
            }

            _ips.TryRemove(key, out _);
            SaveFireAndForget();
            return null;
        }

        public BanEntry? GetFriendCodeBan(string? fc)
        {
            if (fc == null || !_friendCodes.TryGetValue(fc, out var entry))
            {
                return null;
            }

            if (!entry.IsExpired)
            {
                return entry;
            }

            _friendCodes.TryRemove(fc, out _);
            SaveFireAndForget();
            return null;
        }

        public BanEntry BanIp(IPAddress ip, string reason, DateTime? expiresAt)
        {
            var key = Normalize(ip);
            var entry = new BanEntry { Reason = reason, BannedAt = DateTime.UtcNow, BannedUntil = expiresAt };
            _ips[key] = entry;
            SaveFireAndForget();
            return entry;
        }

        public BanEntry BanFriendCode(string fc, string reason, DateTime? expiresAt)
        {
            var entry = new BanEntry { Reason = reason, BannedAt = DateTime.UtcNow, BannedUntil = expiresAt };
            _friendCodes[fc] = entry;
            SaveFireAndForget();
            return entry;
        }

        public bool UnbanIp(IPAddress ip)
        {
            var key = Normalize(ip);
            var removed = _ips.TryRemove(key, out _);
            if (removed)
            {
                SaveFireAndForget();
            }

            return removed;
        }

        public bool UnbanFriendCode(string fc)
        {
            var removed = _friendCodes.TryRemove(fc, out _);
            if (removed)
            {
                SaveFireAndForget();
            }

            return removed;
        }

        public (int IpCount, int FriendCodeCount) Stats()
        {
            CleanExpired();
            return (_ips.Count, _friendCodes.Count);
        }

        public List<BanEntry> AllIpBans()
        {
            CleanExpired();
            return _ips.Values.ToList();
        }

        public List<BanEntry> AllFriendCodeBans()
        {
            CleanExpired();
            return _friendCodes.Values.ToList();
        }

        private void CleanExpired()
        {
            foreach (var kv in _ips)
            {
                if (kv.Value.IsExpired)
                {
                    _ips.TryRemove(kv.Key, out _);
                }
            }

            foreach (var kv in _friendCodes)
            {
                if (kv.Value.IsExpired)
                {
                    _friendCodes.TryRemove(kv.Key, out _);
                }
            }
        }

        private static string Normalize(IPAddress ip)
            => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

        public static DateTime? ParseDuration(string? duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
            {
                return null;
            }

            var match = Regex.Match(duration.Trim(), @"^(\d+)([smhd])$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var value = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLowerInvariant()[0];
            var span = unit switch
            {
                's' => TimeSpan.FromSeconds(value),
                'm' => TimeSpan.FromMinutes(value),
                'h' => TimeSpan.FromHours(value),
                'd' => TimeSpan.FromDays(value),
                _ => TimeSpan.Zero,
            };

            return DateTime.UtcNow.Add(span);
        }

        protected override BanData GetSnapshot()
        {
            CleanExpired();
            return new BanData
            {
                Ips = _ips.ToDictionary(kv => kv.Key, kv => kv.Value),
                FriendCodes = _friendCodes.ToDictionary(kv => kv.Key, kv => kv.Value),
            };
        }

        protected override void ApplySnapshot(BanData data)
        {
            _ips = new ConcurrentDictionary<string, BanEntry>(data.Ips ?? new Dictionary<string, BanEntry>());
            _friendCodes = new ConcurrentDictionary<string, BanEntry>(data.FriendCodes ?? new Dictionary<string, BanEntry>());
        }

        public sealed class BanData
        {
            public Dictionary<string, BanEntry> Ips { get; set; } = new();

            public Dictionary<string, BanEntry> FriendCodes { get; set; } = new();
        }

        public sealed class BanEntry
        {
            public string Reason { get; set; } = string.Empty;

            public DateTime BannedAt { get; set; }

            public DateTime? BannedUntil { get; set; }

            [JsonIgnore]
            public bool IsExpired => BannedUntil.HasValue && DateTime.UtcNow > BannedUntil.Value;
        }
    }
}

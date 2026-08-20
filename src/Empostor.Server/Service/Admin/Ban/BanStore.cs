using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Admin.Ban
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

        public BanEntry BanIp(IPAddress ip, string reason, DateTime? bannedUntil = null)
        {
            var key = Normalize(ip);
            var entry = new BanEntry { Value = key, Reason = reason, BannedAt = DateTime.UtcNow, BannedUntil = bannedUntil };
            _ips[key] = entry;
            SaveFireAndForget();
            return entry;
        }

        public bool UnbanIp(string key)
        {
            var r = _ips.TryRemove(key, out _);
            if (r)
            {
                SaveFireAndForget();
            }

            return r;
        }

        public BanEntry BanFriendCode(string fc, string reason, DateTime? bannedUntil = null)
        {
            var entry = new BanEntry { Value = fc, Reason = reason, BannedAt = DateTime.UtcNow, BannedUntil = bannedUntil };
            _friendCodes[fc] = entry;
            SaveFireAndForget();
            return entry;
        }

        public bool UnbanFriendCode(string key)
        {
            var r = _friendCodes.TryRemove(key, out _);
            if (r)
            {
                SaveFireAndForget();
            }

            return r;
        }

        public IReadOnlyList<BanEntry> AllIpBans()
        {
            if (PruneExpired())
            {
                SaveFireAndForget();
            }

            return _ips.Values.OrderByDescending(b => b.BannedAt).ToList();
        }

        public IReadOnlyList<BanEntry> AllFriendCodeBans()
        {
            if (PruneExpired())
            {
                SaveFireAndForget();
            }

            return _friendCodes.Values.OrderByDescending(b => b.BannedAt).ToList();
        }

        public (int IpCount, int FcCount) Stats()
        {
            if (PruneExpired())
            {
                SaveFireAndForget();
            }

            return (_ips.Count, _friendCodes.Count);
        }

        /// <summary>
        ///     Parses a ban duration string into a UTC expiry timestamp.
        ///     Supported formats: "permanent"/"forever" (or empty) for a permanent ban,
        ///     or "&lt;number&gt;&lt;unit&gt;" where unit is h (hours), d (days), mo (months) or y (years).
        ///     Returns null for a permanent ban.
        /// </summary>
        public static DateTime? ParseDuration(string? duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
            {
                return null;
            }

            var value = duration.Trim();
            if (value.Equals("permanent", StringComparison.OrdinalIgnoreCase)
                || value.Equals("forever", StringComparison.OrdinalIgnoreCase)
                || value.Equals("perm", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var match = Regex.Match(value, @"^(\d+)(h|d|mo|y)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var amount = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLowerInvariant();
            var now = DateTime.UtcNow;

            return unit switch
            {
                "h" => now.AddHours(amount),
                "d" => now.AddDays(amount),
                "mo" => now.AddMonths(amount),
                "y" => now.AddYears(amount),
                _ => null,
            };
        }

        protected override BanData GetSnapshot()
        {
            PruneExpired();
            return new()
            {
                Ips = new(_ips),
                FriendCodes = new(_friendCodes),
            };
        }

        protected override void ApplySnapshot(BanData data)
        {
            _ips = new(data.Ips ?? new());
            _friendCodes = new(data.FriendCodes ?? new());
        }

        private bool PruneExpired()
        {
            var changed = false;
            foreach (var (key, entry) in _ips)
            {
                if (entry.IsExpired && _ips.TryRemove(key, out _))
                {
                    changed = true;
                }
            }

            foreach (var (key, entry) in _friendCodes)
            {
                if (entry.IsExpired && _friendCodes.TryRemove(key, out _))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static string Normalize(IPAddress ip)
            => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

        public sealed class BanData
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

        [JsonPropertyName("bannedUntil")]
        public DateTime? BannedUntil { get; init; }

        [JsonIgnore]
        public bool IsExpired => BannedUntil.HasValue && BannedUntil.Value <= DateTime.UtcNow;

        [JsonIgnore]
        public bool IsPermanent => !BannedUntil.HasValue;
    }
}

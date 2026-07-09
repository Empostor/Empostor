using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
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

        public bool IsIpBanned(IPAddress ip) => _ips.ContainsKey(Normalize(ip));

        public bool IsFriendCodeBanned(string? fc) => fc != null && _friendCodes.ContainsKey(fc);

        public BanEntry BanIp(IPAddress ip, string reason)
        {
            var key = Normalize(ip);
            var entry = new BanEntry { Value = key, Reason = reason, BannedAt = DateTime.UtcNow };
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

        public BanEntry BanFriendCode(string fc, string reason)
        {
            var entry = new BanEntry { Value = fc, Reason = reason, BannedAt = DateTime.UtcNow };
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
            => _ips.Values.OrderByDescending(b => b.BannedAt).ToList();

        public IReadOnlyList<BanEntry> AllFriendCodeBans()
            => _friendCodes.Values.OrderByDescending(b => b.BannedAt).ToList();

        public (int IpCount, int FcCount) Stats() => (_ips.Count, _friendCodes.Count);

        protected override BanData GetSnapshot() => new()
        {
            Ips = new(_ips),
            FriendCodes = new(_friendCodes),
        };

        protected override void ApplySnapshot(BanData data)
        {
            _ips = new(data.Ips ?? new());
            _friendCodes = new(data.FriendCodes ?? new());
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
    }
}

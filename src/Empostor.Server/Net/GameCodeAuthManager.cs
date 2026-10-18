using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Empostor.Api.Config;
using Empostor.Api.Games;
using Empostor.Server.Service.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Net;

// GameCode 認証モード: 動的 UDP ポートを使わず、サーバー発行の一時 GameCode
// （チャレンジコード）で認証情報と UDP 接続を対応付ける。
public sealed class GameCodeAuthManager : IDisposable
{
    // 実コードあたりに預かる旧経路の保留数上限。
    private const int MaxPendingPerCode = 64;

    private readonly ILogger<GameCodeAuthManager> _logger;
    private readonly TimeSpan _lifetime;

    private readonly object _gate = new();

    // チャレンジコード -> チャレンジ内容
    private readonly Dictionary<int, Challenge> _challenges = new();

    // 旧経路: 実コード -> HTTP の加入要求で預かった認証情報
    private readonly Dictionary<GameCode, List<PendingJoin>> _pendingJoins = new();

    // 旧経路: 送信元 IP -> HTTP の募集要求で預かった認証情報
    private readonly Dictionary<string, List<PendingHost>> _hosts = new();

    private readonly Timer _cleanupTimer;

    public GameCodeAuthManager(
        ILogger<GameCodeAuthManager> logger,
        IOptions<ServerConfig> serverConfig)
    {
        _logger = logger;
        _lifetime = serverConfig.Value.TemporaryGameCodeLifetime;

        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private bool Expired(DateTime createdAt) => DateTime.UtcNow - createdAt > _lifetime;

    // 一覧に載せるチャレンジコードを発行する。
    public GameCode CreateChallenge(GameCode targetCode, UserAuthInfo user)
    {
        lock (_gate)
        {
            var code = NewChallengeCode();

            _challenges[code.Value] = new Challenge
            {
                Code = code,
                TargetCode = targetCode,
                User = user,
                Name = user.Name,
                GameVersion = user.ClientVersion,
                CreatedAt = DateTime.UtcNow,
            };

            return code;
        }
    }

    // チャレンジコード -> 実コード（消費しない）。一覧や PlatformId 照会の逆引き用。
    public bool TryGetTarget(GameCode code, out GameCode targetCode)
    {
        lock (_gate)
        {
            if (_challenges.TryGetValue(code.Value, out var challenge) && !Expired(challenge.CreatedAt))
            {
                targetCode = challenge.TargetCode;
                return true;
            }
        }

        targetCode = default;
        return false;
    }

    // UDP JoinGame のコードから認証情報を解決する。
    public bool TryResolveChallenge(GameCode code, int clientId, out UserAuthInfo? user, out GameCode targetCode,
        out string reason)
    {
        user = null;
        targetCode = default;

        lock (_gate)
        {
            if (!_challenges.TryGetValue(code.Value, out var challenge))
            {
                reason = "unknown challenge code";
                return false;
            }

            if (Expired(challenge.CreatedAt))
            {
                _challenges.Remove(code.Value);
                reason = "challenge expired";
                return false;
            }

            if (challenge.OccupiedBy != null && challenge.OccupiedBy != clientId)
            {
                reason = $"challenge already used by client {challenge.OccupiedBy}";
                return false;
            }

            challenge.OccupiedBy = clientId;
            user = challenge.User;
            targetCode = challenge.TargetCode;
            reason = $"challenge matched (target {challenge.TargetCode.Code})";
            return true;
        }
    }

    // 旧経路（POST /api/games）: 実コード宛ての加入要求として認証情報を預かる。
    public void RegisterPendingJoin(GameCode realCode, UserAuthInfo user)
    {
        lock (_gate)
        {
            if (!_pendingJoins.TryGetValue(realCode, out var list))
            {
                list = new List<PendingJoin>();
                _pendingJoins[realCode] = list;
            }

            list.RemoveAll(x => Expired(x.CreatedAt));

            var existing = list.FindIndex(x => x.User.ProductUserId == user.ProductUserId);
            if (existing >= 0)
            {
                list[existing].CreatedAt = DateTime.UtcNow;
                list[existing].Ip = user.ClientIp;
                return;
            }

            if (list.Count >= MaxPendingPerCode)
            {
                // 溢れたら最も古いものを捨てる。
                list.RemoveAt(0);
            }

            list.Add(new PendingJoin
            {
                User = user,
                Name = user.Name,
                GameVersion = user.ClientVersion,
                Ip = user.ClientIp,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    // 旧経路: 実コードのまま JoinGame してきた接続を、IP / 名前 / バージョンで突き合わせる。
    public bool TryConsumePendingJoin(GameCode realCode, string udpIp, string playerName, int gameVersion,
        out UserAuthInfo? user, out string matchInfo)
    {
        user = null;

        lock (_gate)
        {
            if (!_pendingJoins.TryGetValue(realCode, out var list))
            {
                matchInfo = "no pending join for this code";
                return false;
            }

            list.RemoveAll(x => Expired(x.CreatedAt));
            if (list.Count == 0)
            {
                _pendingJoins.Remove(realCode);
                matchInfo = "pending join expired";
                return false;
            }

            var best = PickBest(list.Select(x => (x.Ip, x.Name, x.GameVersion, x.CreatedAt)), udpIp, playerName, gameVersion, out var signals);
            if (best < 0)
            {
                matchInfo = $"no entry matched (ip={Normalize(udpIp) ?? "?"} name=\"{playerName}\" version={gameVersion}, {list.Count} pending)";
                return false;
            }

            user = list[best].User;
            matchInfo = $"{signals} of {list.Count} pending";
            list.RemoveAt(best);

            if (list.Count == 0)
            {
                _pendingJoins.Remove(realCode);
            }

            return true;
        }
    }

    // 募集開始（PUT /api/games）。ゲームもコードもまだ無いので、送信元 IP を鍵に預かる。
    public void RegisterHost(UserAuthInfo user, string? ip)
    {
        var key = Normalize(ip) ?? "(unknown)";

        lock (_gate)
        {
            if (!_hosts.TryGetValue(key, out var list))
            {
                list = new List<PendingHost>();
                _hosts[key] = list;
            }

            list.RemoveAll(x => Expired(x.CreatedAt));

            var existing = list.FindIndex(x => x.User.ProductUserId == user.ProductUserId);
            if (existing >= 0)
            {
                list[existing].CreatedAt = DateTime.UtcNow;
                return;
            }

            list.Add(new PendingHost
            {
                User = user,
                Name = user.Name,
                GameVersion = user.ClientVersion,
                Ip = key,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    // HostGame で接続してきたクライアントに合う保留募集主を 1 件取り出す。
    public bool TryConsumeHost(string udpIp, string playerName, int gameVersion, out UserAuthInfo? user,
        out string matchInfo)
    {
        user = null;
        var key = Normalize(udpIp) ?? "(unknown)";

        lock (_gate)
        {
            if (!_hosts.TryGetValue(key, out var list))
            {
                matchInfo = $"no pending host for ip={key}";
                return false;
            }

            list.RemoveAll(x => Expired(x.CreatedAt));
            if (list.Count == 0)
            {
                _hosts.Remove(key);
                matchInfo = "pending host expired";
                return false;
            }

            var best = PickBest(list.Select(x => (x.Ip, x.Name, x.GameVersion, x.CreatedAt)), udpIp, playerName, gameVersion, out var signals);
            if (best < 0)
            {
                matchInfo = $"no entry matched (name=\"{playerName}\" version={gameVersion}, {list.Count} pending)";
                return false;
            }

            user = list[best].User;
            matchInfo = $"{signals} of {list.Count} pending";
            list.RemoveAt(best);

            if (list.Count == 0)
            {
                _hosts.Remove(key);
            }

            return true;
        }
    }

    // 重複しないチャレンジコードを作る（実コードと同じ V2 形式）。
    private GameCode NewChallengeCode()
    {
        for (var i = 0; i < 64; i++)
        {
            var code = GameCode.Create();
            if (!code.IsInvalid && !_challenges.ContainsKey(code.Value))
            {
                return code;
            }
        }

        throw new InvalidOperationException("failed to allocate a unique challenge code");
    }

    // 最適な 1 件を選ぶ。IP 一致 > 名前 > バージョン、同点なら古い方。
    private static int PickBest(
        IEnumerable<(string? Ip, string Name, int GameVersion, DateTime CreatedAt)> candidates,
        string udpIp,
        string playerName,
        int gameVersion,
        out string signals)
    {
        var list = candidates.ToList();
        var normalizedIp = Normalize(udpIp);

        var bestIndex = -1;
        var bestScore = 0;
        var bestCreated = DateTime.MinValue;
        var bestSignals = string.Empty;

        for (var i = 0; i < list.Count; i++)
        {
            var (ip, name, version, created) = list[i];

            var ipMatch = !string.IsNullOrEmpty(ip) && ip == normalizedIp;
            var nameMatch = !string.IsNullOrEmpty(name)
                            && string.Equals(name, playerName, StringComparison.OrdinalIgnoreCase);
            var versionMatch = version > 0 && version == gameVersion;

            if (!ipMatch && !(nameMatch && versionMatch))
            {
                continue;
            }

            var candidateScore = 0;
            if (ipMatch) candidateScore += 4;
            if (nameMatch) candidateScore += 2;
            if (versionMatch) candidateScore += 1;

            if (candidateScore > bestScore
                || (candidateScore == bestScore && (bestIndex < 0 || created < bestCreated)))
            {
                bestIndex = i;
                bestScore = candidateScore;
                bestCreated = created;
                bestSignals = $"ip{(ipMatch ? "+" : "-")} name{(nameMatch ? "+" : "-")} ver{(versionMatch ? "+" : "-")}";
            }
        }

        signals = bestSignals;
        return bestIndex;
    }

    private void Cleanup()
    {
        try
        {
            lock (_gate)
            {
                foreach (var value in _challenges.Where(x => Expired(x.Value.CreatedAt)).Select(x => x.Key).ToList())
                {
                    _challenges.Remove(value);
                }

                foreach (var code in _pendingJoins.Keys.ToList())
                {
                    var list = _pendingJoins[code];
                    list.RemoveAll(x => Expired(x.CreatedAt));
                    if (list.Count == 0)
                    {
                        _pendingJoins.Remove(code);
                    }
                }

                foreach (var ip in _hosts.Keys.ToList())
                {
                    var list = _hosts[ip];
                    list.RemoveAll(x => Expired(x.CreatedAt));
                    if (list.Count == 0)
                    {
                        _hosts.Remove(ip);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameCodeAuth cleanup failed");
        }
    }

    private static string? Normalize(string? ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            return null;
        }

        return ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase) ? ip[7..] : ip;
    }

    public void Dispose() => _cleanupTimer.Dispose();

    private sealed class Challenge
    {
        public required GameCode Code { get; init; }

        public required GameCode TargetCode { get; init; }

        public required UserAuthInfo User { get; init; }

        public required string Name { get; init; }

        public required int GameVersion { get; init; }

        public required DateTime CreatedAt { get; set; }

        // チャレンジを消費した接続。1 つのチャレンジは 1 接続だけが使える。
        public int? OccupiedBy { get; set; }
    }

    private sealed class PendingJoin
    {
        public required UserAuthInfo User { get; init; }

        public required string Name { get; init; }

        public required int GameVersion { get; init; }

        public required string? Ip { get; set; }

        public required DateTime CreatedAt { get; set; }
    }

    private sealed class PendingHost
    {
        public required UserAuthInfo User { get; init; }

        public required string Name { get; init; }

        public required int GameVersion { get; init; }

        public required string? Ip { get; init; }

        public required DateTime CreatedAt { get; set; }
    }
}

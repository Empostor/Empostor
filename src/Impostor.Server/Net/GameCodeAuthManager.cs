using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.Service.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Net;

public sealed class GameCodeAuthManager : IDisposable
{
    private const int HostKey = 0;
    private const int MaxWaitingPerCode = 64;
    private const int AliasAttempts = 16;
    private static readonly TimeSpan HostLifetime = TimeSpan.FromSeconds(15);

    private readonly ILogger<GameCodeAuthManager> _logger;
    private readonly TimeSpan _lifetime;
    private readonly object _gate = new();
    private readonly Dictionary<int, Ticket> _aliases = new();
    private readonly Dictionary<(string Token, int Target), int> _aliasOwners = new();
    private readonly ConcurrentDictionary<int, List<Ticket>> _waiting = new();
    private readonly Timer _cleanupTimer;

    public GameCodeAuthManager(ILogger<GameCodeAuthManager> logger, IOptions<ServerConfig> serverConfig)
    {
        _logger = logger;
        _lifetime = serverConfig.Value.TemporaryGameCodeLifetime;
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public bool IsAlias(GameCode code)
    {
        lock (_gate)
        {
            RemoveExpiredAliases();
            return _aliases.ContainsKey(code.Value);
        }
    }

    public GameCode IssueAlias(UserAuthInfo user, GameCode targetCode, Func<int, bool> isCodeInUse)
    {
        lock (_gate)
        {
            RemoveExpiredAliases();
            var owner = (user.MatchmakerToken, targetCode.Value);
            if (_aliasOwners.TryGetValue(owner, out var existingCode)
                && _aliases.TryGetValue(existingCode, out var existing)
                && existing.ExpiresAt > DateTime.UtcNow)
            {
                return new GameCode(existingCode);
            }

            _aliasOwners.Remove(owner);
            for (var attempt = 0; attempt < AliasAttempts; attempt++)
            {
                var alias = GameCodeParser.GenerateCode(6);
                if (isCodeInUse(alias) || _aliases.ContainsKey(alias))
                {
                    continue;
                }

                _aliases.Add(alias, new Ticket(user, targetCode.Value, DateTime.UtcNow + _lifetime));
                _aliasOwners[owner] = alias;
                return new GameCode(alias);
            }

            _logger.LogWarning("Could not issue a temporary GameCode alias for {Name}; falling back to name matching", user.Name);
            TryExpectJoin(user, targetCode);
            return targetCode;
        }
    }

    public bool TryReserveGameCode(GameCode code, Func<bool> tryAddGame)
    {
        lock (_gate)
        {
            RemoveExpiredAliases();
            return !_aliases.ContainsKey(code.Value) && tryAddGame();
        }
    }

    public bool TryExpectHost(UserAuthInfo user)
        => Enqueue(HostKey, user, HostKey, _lifetime < HostLifetime ? _lifetime : HostLifetime);

    public bool TryExpectJoin(UserAuthInfo user, GameCode targetCode)
        => Enqueue(targetCode.Value, user, targetCode.Value, _lifetime);

    public UserAuthInfo? ClaimHost(string playerName)
        => ClaimWaiting(HostKey, playerName)?.User;

    public (UserAuthInfo User, GameCode Target, int Alias)? ClaimJoin(GameCode joinCode, string playerName)
    {
        lock (_gate)
        {
            RemoveExpiredAliases();
            if (_aliases.Remove(joinCode.Value, out var alias))
            {
                _aliasOwners.Remove((alias.User.MatchmakerToken, alias.Target));
                return (alias.User, new GameCode(alias.Target), joinCode.Value);
            }
        }

        var pending = ClaimWaiting(joinCode.Value, playerName);
        return pending == null ? null : (pending.User, new GameCode(pending.Target), 0);
    }

    public void Dispose() => _cleanupTimer.Dispose();

    private bool Enqueue(int key, UserAuthInfo user, int target, TimeSpan lifetime)
    {
        var list = _waiting.GetOrAdd(key, static _ => new List<Ticket>());
        lock (list)
        {
            RemoveExpired(list);
            if (list.Count >= MaxWaitingPerCode)
            {
                _logger.LogWarning("GameCode authentication queue is full for {Key}", key);
                return false;
            }

            foreach (var other in list)
            {
                if (string.Equals(other.User.Name, user.Name, StringComparison.Ordinal)
                    && !string.Equals(other.User.ProductUserId, user.ProductUserId, StringComparison.Ordinal))
                {
                    _logger.LogWarning("{Name} is already queued by a different account", user.Name);
                    return false;
                }
            }

            list.RemoveAll(ticket => string.Equals(ticket.User.ProductUserId, user.ProductUserId, StringComparison.Ordinal));
            list.Add(new Ticket(user, target, DateTime.UtcNow + lifetime));
            return true;
        }
    }

    private Ticket? ClaimWaiting(int key, string playerName)
    {
        if (!_waiting.TryGetValue(key, out var list)) return null;
        lock (list)
        {
            RemoveExpired(list);
            var index = list.FindIndex(ticket => string.Equals(ticket.User.Name, playerName, StringComparison.Ordinal));
            if (index < 0) return null;
            var ticket = list[index];
            list.RemoveAt(index);
            return ticket;
        }
    }

    private void Cleanup()
    {
        try
        {
            lock (_gate) RemoveExpiredAliases();
            foreach (var list in _waiting.Values)
            {
                lock (list) RemoveExpired(list);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameCode authentication cleanup failed");
        }
    }

    private void RemoveExpiredAliases()
    {
        var now = DateTime.UtcNow;
        foreach (var (code, ticket) in _aliases.ToArray())
        {
            if (ticket.ExpiresAt <= now)
            {
                _aliases.Remove(code);
                _aliasOwners.Remove((ticket.User.MatchmakerToken, ticket.Target));
            }
        }
    }

    private static void RemoveExpired(List<Ticket> list) => list.RemoveAll(ticket => ticket.ExpiresAt <= DateTime.UtcNow);

    private sealed record Ticket(UserAuthInfo User, int Target, DateTime ExpiresAt);
}
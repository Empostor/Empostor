using System;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Events;
using Empostor.Api.Games;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FixedCode;

public sealed class FixedCodeListener : IEventListener
{
    private readonly ILogger<FixedCodeListener> _logger;

    private readonly FixedCodeStore _store;

    private Dictionary<string, GameCode> _map;

    public int MappingCount => _map.Count;

    public FixedCodeListener(ILogger<FixedCodeListener> logger, FixedCodeStore store)
    {
        _logger = logger;
        _store = store;
        _map = BuildMap(_store.Mappings, logger);
    }

    /// <summary>Rebuilds the in-memory map after the admin panel changed the mappings.</summary>
    public void Rebuild() => _map = BuildMap(_store.Mappings, _logger);

    [EventListener]
    public void OnGameCreation(IGameCreationEvent e)
    {
        var friendCode = e.Client?.FriendCode;
        if (string.IsNullOrEmpty(friendCode)) return;

        if (!_map.TryGetValue(friendCode, out var code)) return;

        e.GameCode = code;
        _logger.LogInformation(
            "[FixedCode] Assigned room code {Code} to host with FriendCode {FC}",
            code.Code, friendCode);
    }

    private static Dictionary<string, GameCode> BuildMap(
        IEnumerable<FriendCodeMapping> mappings,
        ILogger logger)
    {
        var map = new Dictionary<string, GameCode>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in mappings)
        {
            if (string.IsNullOrWhiteSpace(m.FriendCode) ||
                string.IsNullOrWhiteSpace(m.RoomCode))
            {
                logger.LogWarning("[FixedCode] Skipping empty mapping entry.");
                continue;
            }

            var upper = m.RoomCode.ToUpperInvariant();
            if (upper.Length != 4 && upper.Length != 6)
            {
                logger.LogWarning(
                    "[FixedCode] Room code '{Code}' for {FC} is not 4 or 6 characters — skipped.",
                    m.RoomCode, m.FriendCode);
                continue;
            }

            if (!upper.All(char.IsLetter))
            {
                logger.LogWarning(
                    "[FixedCode] Room code '{Code}' for {FC} contains non-letter characters — skipped.",
                    m.RoomCode, m.FriendCode);
                continue;
            }

            var gameCode = new GameCode(upper);
            if (gameCode.IsInvalid)
            {
                logger.LogWarning(
                    "[FixedCode] Room code '{Code}' for {FC} is invalid — skipped.",
                    m.RoomCode, m.FriendCode);
                continue;
            }

            map[m.FriendCode] = gameCode;
            logger.LogInformation(
                "[FixedCode] Mapping: {FC} → {Code}", m.FriendCode, upper);
        }

        return map;
    }
}

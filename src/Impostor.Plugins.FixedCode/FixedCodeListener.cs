using System;
using System.Collections.Generic;
using System.Linq;
using Impostor.Api.Events;
using Impostor.Api.Games;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.FixedCode;

public sealed class FixedCodeListener : IEventListener
{
    private readonly ILogger<FixedCodeListener> _logger;

    private readonly Dictionary<string, GameCode> _map;

    public int MappingCount => _map.Count;

    public FixedCodeListener(ILogger<FixedCodeListener> logger, FixedCodeConfig config)
    {
        _logger = logger;
        _map = BuildMap(config, logger);
    }

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
        FixedCodeConfig config,
        ILogger logger)
    {
        var map = new Dictionary<string, GameCode>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in config.Mappings)
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

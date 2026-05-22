using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empostor.Api.Games;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Code;

public sealed class GameCodeManager : IGameCodeManager
{
    private static readonly HashSet<char> V2Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToHashSet();

    private readonly ILogger<GameCodeManager> _logger;
    private readonly IGameCodeFactory _codeFactory;
    private readonly List<GameCode> _codes;
    private readonly HashSet<GameCode> _inUse;
    private readonly object _sync = new();

    public string Path => System.IO.Path.GetFullPath("Boot.Codes");

    public int SixCharCodes { get; }

    public int FourCharCodes { get; }

    public GameCodeManager(ILogger<GameCodeManager> logger, IGameCodeFactory codeFactory)
    {
        _logger = logger;
        _codeFactory = codeFactory;

        _logger.LogInformation("[Code] Reading files from {Path}", Path);

        var list = Read().ToList();
        if (list.Count == 0)
        {
            _codes = new List<GameCode>();
            _inUse = new HashSet<GameCode>();
            return;
        }

        Extensions.Shuffle(list);

        FourCharCodes = list.Count(c => c.Code.Length == 4);
        SixCharCodes = list.Count(c => c.Code.Length == 6);
        _codes = list;
        _inUse = new HashSet<GameCode>();
    }

    private List<GameCode> Read()
    {
        var dirInfo = new DirectoryInfo(Path);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
            _logger.LogWarning("[Code] No valid word list found. Place .txt files in Boot.Codes/ folder.");
            return new List<GameCode>();
        }

        var seen = new HashSet<GameCode>();

        foreach (var fileInfo in dirInfo.GetFiles())
        {
            _logger.LogInformation("[Code] Reading \"{Name}\"", fileInfo.Name);

            foreach (var rawLine in File.ReadLines(fileInfo.FullName))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--"))
                    continue;

                var codeText = line.Split("--", 2, StringSplitOptions.None)[0].TrimEnd();

                if (codeText.Length != 6 && codeText.Length != 4)
                    continue;

                if (!codeText.All(c => V2Chars.Contains(c)))
                    continue;

                var item = new GameCode(codeText);
                if (!item.IsInvalid)
                    seen.Add(item);
            }
        }

        if (seen.Count > 0)
            _logger.LogInformation("[Code] Finished loading {Count} codes.", seen.Count);

        return seen.ToList();
    }

    public GameCode Get()
    {
        lock (_sync)
        {
            if (_codes.Count == 0)
            {
                _logger.LogWarning("[Code] Ran out of codes — falling back to default code factory.");
                return _codeFactory.Create();
            }

            var index = StrongRandom.Next(0, _codes.Count);
            var gameCode = _codes[index];
            _codes.RemoveAt(index);
            _inUse.Add(gameCode);
            return gameCode;
        }
    }

    public void Release(GameCode code)
    {
        lock (_sync)
        {
            if (_inUse.Remove(code))
                _codes.Add(code);
        }
    }
}

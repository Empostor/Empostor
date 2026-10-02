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
    private readonly object _sync = new();
    private readonly Dictionary<GameCode, string> _source = new();
    private List<GameCode> _codes = new();
    private HashSet<GameCode> _inUse = new();

    private string AdminFilePath => System.IO.Path.Combine(Path, "00-admin-added.txt");

    public string Path => System.IO.Path.GetFullPath("Boot.Codes");

    public int SixCharCodes { get; private set; }

    public int FourCharCodes { get; private set; }

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
        _source.Clear();

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
                {
                    seen.Add(item);
                    _source[item] = fileInfo.Name;
                }
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

    public IReadOnlyList<PoolEntry> Entries
    {
        get
        {
            lock (_sync)
            {
                var entries = new List<PoolEntry>(_codes.Count + _inUse.Count);
                foreach (var code in _codes) entries.Add(new PoolEntry(code, false, SourceLabel(code)));
                foreach (var code in _inUse) entries.Add(new PoolEntry(code, true, SourceLabel(code)));
                entries.Sort((a, b) => string.Compare(a.Code.Code, b.Code.Code, StringComparison.OrdinalIgnoreCase));
                return entries;
            }
        }
    }

    public (bool Ok, string Message) Add(string codeText)
    {
        var text = codeText.Trim().ToUpperInvariant();
        if (text.Length != 4 && text.Length != 6)
            return (false, "Room code must be 4 or 6 letters.");

        if (!text.All(c => V2Chars.Contains(c)))
            return (false, "Room code may only contain letters.");

        var code = new GameCode(text);
        if (code.IsInvalid)
            return (false, "Not a valid room code.");

        lock (_sync)
        {
            if (_codes.Contains(code) || _inUse.Contains(code))
                return (false, $"{text} is already in the pool.");

            _codes.Add(code);
            _source[code] = AdminFilePath;
        }

        // Persist so admin-added codes survive a restart (Read() merges this file back in).
        try
        {
            File.AppendAllLines(AdminFilePath, new[] { text });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Code] Failed to append {Code} to {File}", text, AdminFilePath);
        }

        return (true, $"Code {text} added to the pool.");
    }

    public (bool Ok, string Message) Remove(GameCode code)
    {
        lock (_sync)
        {
            var fromAvailable = _codes.Remove(code);
            var fromInUse = _inUse.Remove(code);
            if (!fromAvailable && !fromInUse)
                return (false, "That code is not in the pool.");

            var note = string.Empty;
            if (_source.TryGetValue(code, out var source) && source != AdminFilePath)
                note = $" It comes from {source} — remove it there too, or it will return on reload.";

            _source.Remove(code);

            try
            {
                RemoveFromAdminFile(code.Code);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Code] Failed to remove {Code} from {File}", code.Code, AdminFilePath);
            }

            return (true, $"Code {code.Code} removed." + note);
        }
    }

    public int Reload()
    {
        lock (_sync)
        {
            var kept = new List<GameCode>(_inUse);
            _source.Clear();

            var list = Read();
            foreach (var code in kept)
            {
                if (!list.Contains(code)) list.Add(code);
                if (!_source.ContainsKey(code)) _source[code] = "(in use)";
            }

            Extensions.Shuffle(list);
            FourCharCodes = list.Count(c => c.Code.Length == 4);
            SixCharCodes = list.Count(c => c.Code.Length == 6);
            _codes = list;
            _inUse = new HashSet<GameCode>(kept);
            return _codes.Count;
        }
    }

    private string SourceLabel(GameCode code)
        => _source.TryGetValue(code, out var source) ? source : string.Empty;

    private void RemoveFromAdminFile(string code)
    {
        var file = AdminFilePath;
        if (!File.Exists(file)) return;

        var kept = File.ReadAllLines(file)
            .Where(l => !l.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        File.WriteAllLines(file, kept);
    }
}

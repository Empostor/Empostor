using System;
using System.Collections.Generic;
using Impostor.Api.Config;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Service.Admin.Chat;

public sealed class ChatFilterStore
{
    private readonly object _wordsLock = new();

    private List<string> _blockedWords;

    public ChatFilterStore(IOptions<ChatFilterConfig> config)
    {
        var cfg = config.Value;
        Enabled = cfg.Enabled;
        BlockMessage = cfg.BlockMessage;
        SpamThreshold = cfg.SpamThreshold;
        SpamWindowSeconds = cfg.SpamWindowSeconds;
        _blockedWords = new List<string>(cfg.BlockedWords ?? new List<string>());
    }

    public bool Enabled { get; set; }

    public bool BlockMessage { get; set; }

    public int SpamThreshold { get; set; }

    public int SpamWindowSeconds { get; set; }

    public IReadOnlyList<string> BlockedWords
    {
        get
        {
            lock (_wordsLock)
            {
                return _blockedWords.AsReadOnly();
            }
        }
    }

    public void AddWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return;
        }

        var trimmed = word.Trim();
        lock (_wordsLock)
        {
            if (_blockedWords.Exists(w => w.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _blockedWords.Add(trimmed);
        }
    }

    public bool RemoveWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        lock (_wordsLock)
        {
            return _blockedWords.RemoveAll(
                w => w.Equals(word.Trim(), StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }
}

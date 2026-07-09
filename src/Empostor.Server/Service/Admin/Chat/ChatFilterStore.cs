using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Admin.Chat;

public sealed class ChatFilterStore : JsonDataStore<ChatFilterConfig>
{
    private readonly object _wordsLock = new();

    private List<string> _blockedWords = new();

    public ChatFilterStore(ILogger<ChatFilterStore> logger, IOptions<ChatFilterConfig> config)
        : base(logger, legacyPath: null)
    {
        Load();

        // If no persisted file was loaded, fall back to config.json defaults
        if (_blockedWords.Count == 0 && !Enabled)
        {
            var cfg = config.Value;
            Enabled = cfg.Enabled;
            BlockMessage = cfg.BlockMessage;
            SpamThreshold = cfg.SpamThreshold;
            SpamWindowSeconds = cfg.SpamWindowSeconds;
            _blockedWords = new List<string>(cfg.BlockedWords ?? new List<string>());
        }
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

        SaveFireAndForget();
    }

    public bool RemoveWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        var removed = false;
        lock (_wordsLock)
        {
            removed = _blockedWords.RemoveAll(
                w => w.Equals(word.Trim(), StringComparison.OrdinalIgnoreCase)) > 0;
        }

        if (removed)
        {
            SaveFireAndForget();
        }

        return removed;
    }

    public new async ValueTask SaveAsync() => await base.SaveAsync();

    protected override ChatFilterConfig GetSnapshot()
    {
        lock (_wordsLock)
        {
            return new ChatFilterConfig
            {
                Enabled = Enabled,
                BlockMessage = BlockMessage,
                SpamThreshold = SpamThreshold,
                SpamWindowSeconds = SpamWindowSeconds,
                BlockedWords = new List<string>(_blockedWords),
            };
        }
    }

    protected override void ApplySnapshot(ChatFilterConfig data)
    {
        Enabled = data.Enabled;
        BlockMessage = data.BlockMessage;
        SpamThreshold = data.SpamThreshold;
        SpamWindowSeconds = data.SpamWindowSeconds;
        lock (_wordsLock)
        {
            _blockedWords = new List<string>(data.BlockedWords ?? new List<string>());
        }
    }
}

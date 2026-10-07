using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Localization;

internal sealed class LocalizationFileStore
{
    private readonly string contentRoot;
    private readonly ILogger logger;

    public LocalizationFileStore(string contentRoot, ILogger logger)
    {
        this.contentRoot = contentRoot;
        this.logger = logger;
    }

    public IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> Load(string owner, Language defaultLanguage,
        IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> defaults)
    {
        var overrides = new Dictionary<Language, IReadOnlyDictionary<string, string>>();
        var directory = Path.Combine(contentRoot, "languages", owner);
        foreach (var (language, code) in LanguageCodes.All)
        {
            var file = Path.Combine(directory, code + ".json");
            var existing = File.Exists(file) ? LocalizationJson.ReadFile(file, logger) : new Dictionary<string, string>();
            if (existing == null)
                continue;

            if (defaults.TryGetValue(language, out var table))
            {
                var changed = false;
                foreach (var (key, text) in table)
                    changed |= existing.TryAdd(key, text);
                if (changed)
                    Write(file, existing);
            }

            var valid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, text) in existing)
            {
                if (defaults[defaultLanguage].TryGetValue(key, out var fallback) && LocalizationJson.ValidArguments(text, fallback))
                    valid.Add(key, text);
                else
                    logger.LogWarning("Unknown localization key or invalid arguments for {Key} in {File}", key, file);
            }

            overrides.Add(language, valid);
        }

        return overrides;
    }

    private void Write(string file, Dictionary<string, string> values)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(values, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }
        catch (IOException ex) { logger.LogWarning("Cannot write localization file {File}: {Reason}", file, ex.Message); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning("Cannot access localization file {File}: {Reason}", file, ex.Message); }
    }
}

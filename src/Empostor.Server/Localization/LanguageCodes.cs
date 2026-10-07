using System;
using System.Collections.Generic;
using Empostor.Api.Innersloth;

namespace Empostor.Server.Localization;

internal static class LanguageCodes
{
    public static readonly IReadOnlyDictionary<Language, string> All = new Dictionary<Language, string>
    {
        [Language.English] = "en", [Language.Latam] = "es-419", [Language.Brazilian] = "pt-BR",
        [Language.Portuguese] = "pt-PT", [Language.Korean] = "ko", [Language.Russian] = "ru",
        [Language.Dutch] = "nl", [Language.Filipino] = "fil", [Language.French] = "fr",
        [Language.German] = "de", [Language.Italian] = "it", [Language.Japanese] = "ja",
        [Language.Spanish] = "es-ES", [Language.SChinese] = "zh-CN", [Language.TChinese] = "zh-TW",
        [Language.Irish] = "ga",
    };

    public static Language FromTag(string tag)
    {
        var normalized = tag.Replace('_', '-');
        foreach (var (language, code) in All)
        {
            if (string.Equals(normalized, code, StringComparison.OrdinalIgnoreCase))
                return language;
        }

        return normalized.ToLowerInvariant() switch
        {
            "es" => Language.Spanish,
            "pt" => Language.Portuguese,
            _ => Language.English,
        };
    }
}

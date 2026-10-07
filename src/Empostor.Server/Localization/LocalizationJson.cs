using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Localization;

internal static class LocalizationJson
{
    public static Dictionary<string, string>? Parse(string json, string source, ILogger logger)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            logger.LogWarning("Invalid localization JSON in {Source}: {Reason}", source, ex.Message);
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("Localization resource {Source} must contain an object", source);
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                {
                    logger.LogWarning("Invalid translation {Key} in {Source}", property.Name, source);
                    return null;
                }

                var text = property.Value.GetString()!;
                if (!TryFormat(text, out _) || !values.TryAdd(property.Name, text))
                {
                    logger.LogWarning("Duplicate key or invalid format for {Key} in {Source}", property.Name, source);
                    return null;
                }
            }

            return values;
        }
    }

    public static Dictionary<string, string>? ReadFile(string file, ILogger logger)
    {
        string json;
        try { json = File.ReadAllText(file); }
        catch (IOException ex)
        {
            logger.LogWarning("Cannot read localization file {File}: {Reason}", file, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Cannot access localization file {File}: {Reason}", file, ex.Message);
            return null;
        }

        return Parse(json, file, logger);
    }

    public static bool TryFormat(string text, out CompositeFormat? format)
    {
        try { format = CompositeFormat.Parse(text); return true; }
        catch (FormatException) { format = null; return false; }
    }

    public static bool ValidArguments(string value, string fallback) =>
        TryFormat(value, out var format) && TryFormat(fallback, out var defaultFormat)
        && format!.MinimumArgumentCount <= defaultFormat!.MinimumArgumentCount;
}

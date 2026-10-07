using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Empostor.Api.Innersloth;
using Empostor.Api.Localization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Localization;

public sealed class LocalizationService : ILocalizationService, ILocalizationRegistry
{
    private readonly object _gate = new();
    private readonly ILogger<LocalizationService> _logger;
    private readonly LocalizationFileStore _files;
    private readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, LocalizationSnapshot> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService(ILogger<LocalizationService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _files = new LocalizationFileStore(environment.ContentRootPath, logger);
        if (!Register(new LocalizationCatalog("empostor", typeof(LocalizationService).Assembly, "Empostor.Server.Localization.Resources"), out _))
            logger.LogError("Cannot load built-in localization resources");
    }

    public bool TryRegister(LocalizationCatalog catalog, out IDisposable? registration)
    {
        registration = null;
        if (catalog == null || string.Equals(catalog.Owner, "empostor", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Missing localization catalog or reserved owner");
            return false;
        }

        return Register(catalog, out registration);
    }

    public string Get(LocalizedMessageKey message, Language language, params object?[] arguments)
    {
        if (message.Owner != null && message.Key != null && Volatile.Read(ref _catalogs).TryGetValue(message.Owner, out var state))
        {
            var selected = LanguageCodes.All.ContainsKey(language) ? language : state.DefaultLanguage;
            foreach (var text in Candidates(state, selected, message.Key))
            {
                if (!LocalizationJson.TryFormat(text, out var format) || arguments.Length < format!.MinimumArgumentCount)
                    continue;
                try { return string.Format(CultureInfo.GetCultureInfo(LanguageCodes.All[selected]), text, arguments); }
                catch (FormatException ex) { _logger.LogWarning("Cannot format {Message}: {Reason}", message, ex.Message); }
            }
        }

        _logger.LogWarning("Missing localization message or arguments for {Message}", message);
        return message.ToString();
    }

    private bool Register(LocalizationCatalog catalog, out IDisposable? registration)
    {
        registration = null;
        if (string.IsNullOrWhiteSpace(catalog.Owner) || catalog.Owner.Length > 128 || catalog.Owner is "." or ".."
            || catalog.Owner.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            || catalog.Assembly == null || string.IsNullOrWhiteSpace(catalog.ResourcePrefix))
        {
            _logger.LogWarning("Invalid localization catalog owner, assembly or prefix");
            return false;
        }

        lock (_gate)
        {
            if (_initialized.Contains(catalog.Owner))
            {
                _logger.LogWarning("Localization owner {Owner} already initialized; restart to reload", catalog.Owner);
                return false;
            }

            var defaults = new Dictionary<Language, IReadOnlyDictionary<string, string>>();
            foreach (var (language, code) in LanguageCodes.All)
            {
                using var stream = catalog.Assembly.GetManifestResourceStream($"{catalog.ResourcePrefix}.{code}.json");
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                var table = LocalizationJson.Parse(reader.ReadToEnd(), $"{catalog.Owner}/{code}", _logger);
                if (table == null) return false;
                defaults.Add(language, table);
            }

            if (!defaults.TryGetValue(catalog.DefaultLanguage, out var fallback) || fallback.Count == 0)
            {
                _logger.LogWarning("Missing default language JSON for {Owner}", catalog.Owner);
                return false;
            }

            foreach (var table in defaults.Values)
                foreach (var (key, text) in table)
                    if (!fallback.TryGetValue(key, out var defaultText) || !LocalizationJson.ValidArguments(text, defaultText))
                    {
                        _logger.LogWarning("Invalid localization key or arguments for {Owner}:{Key}", catalog.Owner, key);
                        return false;
                    }

            var state = new LocalizationSnapshot(catalog.DefaultLanguage, defaults, _files.Load(catalog.Owner, catalog.DefaultLanguage, defaults));
            var next = new Dictionary<string, LocalizationSnapshot>(_catalogs, StringComparer.OrdinalIgnoreCase) { [catalog.Owner] = state };
            Volatile.Write(ref _catalogs, next);
            _initialized.Add(catalog.Owner);
            registration = new LocalizationRegistration(() => Unregister(catalog.Owner));
            return true;
        }
    }

    private void Unregister(string owner)
    {
        lock (_gate)
        {
            var next = new Dictionary<string, LocalizationSnapshot>(_catalogs, StringComparer.OrdinalIgnoreCase);
            next.Remove(owner);
            Volatile.Write(ref _catalogs, next);
        }
    }

    private static IEnumerable<string> Candidates(LocalizationSnapshot state, Language language, string key)
    {
        if (state.Overrides.TryGetValue(language, out var table) && table.TryGetValue(key, out var text)) yield return text;
        if (state.Defaults.TryGetValue(language, out table) && table.TryGetValue(key, out text)) yield return text;
        if (language != state.DefaultLanguage)
            foreach (var fallback in Candidates(state, state.DefaultLanguage, key)) yield return fallback;
    }
}

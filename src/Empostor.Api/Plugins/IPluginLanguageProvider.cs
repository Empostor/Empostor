using System.Collections.Generic;

namespace Empostor.Api.Plugins;

/// <summary>
/// Implement this on your plugin class to provide translations that are
/// automatically registered with the language service on enable.
/// </summary>
public interface IPluginLanguageProvider
{
    /// <summary>
    /// Returns translations grouped by language code.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations();
}

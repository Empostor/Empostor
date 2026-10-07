using System;

namespace Empostor.Api.Localization;

public interface ILocalizationRegistry
{
    /// <summary>Registers embedded JSON during startup. Files are not reloaded during this session.</summary>
    bool TryRegister(LocalizationCatalog catalog, out IDisposable? registration);
}

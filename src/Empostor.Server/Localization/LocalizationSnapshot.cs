using System.Collections.Generic;
using Empostor.Api.Innersloth;

namespace Empostor.Server.Localization;

internal sealed record LocalizationSnapshot(Language DefaultLanguage,
    IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> Defaults,
    IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> Overrides);

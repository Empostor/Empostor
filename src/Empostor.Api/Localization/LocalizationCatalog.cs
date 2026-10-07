using System.Reflection;
using Empostor.Api.Innersloth;

namespace Empostor.Api.Localization;

/// <summary>Describes embedded JSON resources named prefix.language-code.json.</summary>
public sealed record LocalizationCatalog(string Owner, Assembly Assembly, string ResourcePrefix, Language DefaultLanguage = Language.English);

using Empostor.Api.Innersloth;

namespace Empostor.Api.Localization;

public interface ILocalizationService
{
    string Get(LocalizedMessageKey message, Language language, params object?[] arguments);
}

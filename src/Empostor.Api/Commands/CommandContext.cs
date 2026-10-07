using Empostor.Api.Games;
using Empostor.Api.Localization;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Commands
{
    public sealed class CommandContext
    {
        public required string Name { get; init; }

        public required string RawArgs { get; init; }

        public required string[] Args { get; init; }

        public required IClientPlayer Sender { get; init; }

        public required IInnerPlayerControl PlayerControl { get; init; }

        public required IGame Game { get; init; }

        public required ILocalizationService Localization { get; init; }

        public Innersloth.Language SenderLanguage => Sender.Client.Language;

        public bool IsSenderChinese()
            => SenderLanguage == Innersloth.Language.SChinese ||
               SenderLanguage == Innersloth.Language.TChinese;

        public string GetString(string key, params object?[] arguments)
            => Localization.Get(new LocalizedMessageKey("empostor", key), SenderLanguage, arguments);

        public string GetPluginString(string owner, string key, params object?[] arguments)
            => Localization.Get(new LocalizedMessageKey(owner, key), SenderLanguage, arguments);
    }
}

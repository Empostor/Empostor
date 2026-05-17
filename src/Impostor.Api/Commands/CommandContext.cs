using Impostor.Api.Games;
using Impostor.Api.Languages;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Api.Commands
{
    public sealed class CommandContext
    {
        public required string Name { get; init; }

        public required string RawArgs { get; init; }

        public required string[] Args { get; init; }

        public required IClientPlayer Sender { get; init; }

        public required IInnerPlayerControl PlayerControl { get; init; }

        public required IGame Game { get; init; }

        public required LanguageService Lang { get; init; }

        public Innersloth.Language SenderLanguage => Sender.Client.Language;

        public bool IsSenderChinese()
            => SenderLanguage == Innersloth.Language.SChinese ||
               SenderLanguage == Innersloth.Language.TChinese;

        public LanguageString GetString(string key)
            => Lang.Get(key, SenderLanguage);
    }
}

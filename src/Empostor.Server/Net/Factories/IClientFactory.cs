using Empostor.Api.Net;

namespace Empostor.Server.Net.Factories
{
    internal interface IClientFactory
    {
        ClientBase Create(IHazelConnection connection, string name, GameVersion clientVersion, Language language, QuickChatModes chatMode, PlatformSpecificData platformSpecificData);
    }
}

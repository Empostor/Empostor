using System;
using Empostor.Api.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Empostor.Server.Net.Factories
{
    internal class ClientFactory<TClient> : IClientFactory
        where TClient : ClientBase
    {
        private readonly IServiceProvider _serviceProvider;

        public ClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ClientBase Create(IHazelConnection connection, string name, GameVersion clientVersion, Language language, QuickChatModes chatMode, PlatformSpecificData platformSpecificData)
        {
            var client = ActivatorUtilities.CreateInstance<TClient>(_serviceProvider, name, clientVersion, language, chatMode, platformSpecificData, connection);
            connection.Client = client;
            return client;
        }
    }
}

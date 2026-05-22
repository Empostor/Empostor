using Empostor.Api.Events.Client;
using Empostor.Api.Net;

namespace Empostor.Server.Events.Client
{
    public class ClientConnectedEvent : IClientConnectedEvent
    {
        public ClientConnectedEvent(IHazelConnection connection, IClient client)
        {
            Connection = connection;
            Client = client;
        }

        public IHazelConnection Connection { get; }

        public IClient Client { get; }
    }
}

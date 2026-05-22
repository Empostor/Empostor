using Empostor.Api.Events.Client;
using Empostor.Api.Net;

namespace Empostor.Server.Events.Client
{
    public class ClientConnectionEvent : IClientConnectionEvent
    {
        public ClientConnectionEvent(IHazelConnection connection, IMessageReader handshakeData)
        {
            Connection = connection;
            HandshakeData = handshakeData;
        }

        public IHazelConnection Connection { get; }

        public IMessageReader HandshakeData { get; }
    }
}

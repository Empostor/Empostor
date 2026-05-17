using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net.Messages.C2S;
using Next.Hazel;
using Next.Hazel.Udp;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Hazel;
using Impostor.Server.Net.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Impostor.Server.Net
{
    internal class Matchmaker
    {
        private readonly IEventManager _eventManager;
        private readonly ClientManager _clientManager;
        private readonly ObjectPool<MessageReader> _readerPool;
        private readonly ILogger<HazelConnection> _connectionLogger;
        private readonly ILogger<Matchmaker> _logger;

        private UdpConnectionListener? _gameListener;

        public Matchmaker(
            IEventManager eventManager,
            ClientManager clientManager,
            ObjectPool<MessageReader> readerPool,
            ILogger<HazelConnection> connectionLogger,
            ILogger<Matchmaker> logger)
        {
            _eventManager = eventManager;
            _clientManager = clientManager;
            _readerPool = readerPool;
            _connectionLogger = connectionLogger;
            _logger = logger;
        }

        public async ValueTask StartAsync(IPEndPoint ipEndPoint)
        {
            var mode = ipEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => throw new InvalidOperationException(),
            };

            _gameListener = new UdpConnectionListener(ipEndPoint, _readerPool, mode)
            {
                NewConnection = OnNewConnection,
            };

            await _gameListener.StartAsync();
            _logger.LogInformation("[Matchmaker] UDP listener started on {EP}", ipEndPoint);
        }

        public async ValueTask StopAsync()
        {
            if (_gameListener != null)
            {
                await _gameListener.DisposeAsync();
            }
        }

        private async ValueTask OnNewConnection(NewConnectionEventArgs e)
        {
            HandshakeC2S.Deserialize(
                e.HandshakeData,
                out var clientVersion,
                out var name,
                out var language,
                out var chatMode,
                out var platformSpecificData,
                out var matchmakerToken);

            var connection = new HazelConnection(e.Connection, _connectionLogger);
            await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));
            await _clientManager.RegisterConnectionAsync(
                connection, name, clientVersion, language, chatMode, platformSpecificData, matchmakerToken);
        }
    }
}

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Net
{
    internal class MatchmakerService : IHostedService
    {
        private readonly ILogger<MatchmakerService> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly HttpServerConfig _httpServerConfig;
        private readonly Matchmaker _matchmaker;
        private readonly PortPoolService _portPool;

        public MatchmakerService(
            ILogger<MatchmakerService> logger,
            IOptions<ServerConfig> serverConfig,
            IOptions<HttpServerConfig> httpServerConfig,
            Matchmaker matchmaker,
            PortPoolService portPool)
        {
            _logger = logger;
            _serverConfig = serverConfig.Value;
            _httpServerConfig = httpServerConfig.Value;
            _matchmaker = matchmaker;
            _portPool = portPool;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(_serverConfig.ResolveListenIp()), _serverConfig.ListenPort);

            await _matchmaker.StartAsync(endpoint);

            _logger.LogInformation(
                "Matchmaker is listening on {Address}:{Port}, the public server ip is {PublicIp}:{PublicPort}.",
                endpoint.Address,
                endpoint.Port,
                _serverConfig.ResolvePublicIp(),
                _serverConfig.PublicPort);

            if (_portPool.IsEnabled)
            {
                _logger.LogInformation(
                    "Delta Matchmaker is enabled.");
            }
            else
            {
                _logger.LogInformation(
                    "Delta Matchmaker is disabled (DeltaPortStart={Start}, DeltaPortEnd={End}). Using IP-based matching.",
                    _serverConfig.DeltaPortStart,
                    _serverConfig.DeltaPortEnd);
            }

            var runningOutsideContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == null;
            if (_serverConfig.PublicIp == "127.0.0.1")
            {
                // NOTE: If this warning annoys you, set your PublicIp to "localhost"
                _logger.LogError("Your PublicIp is set to the default value of 127.0.0.1.");
                _logger.LogError("To allow people on other devices to connect to your server, change this value to your Public IP address");
                _logger.LogError("For more info on how to do this see https://empostor.github.io/Server-configuration");
            }
            else if (_httpServerConfig.ListenIp == "0.0.0.0" && runningOutsideContainer)
            {
                _logger.LogWarning("Since Among Us 16.0.5 it is required to support HTTPS for players to connect, we recommend setting up a reverse proxy:");
                _logger.LogWarning("See https://empostor.github.io/Http-server for instructions");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Matchmaker is shutting down!");
            await _matchmaker.StopAsync();
        }
    }
}

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Server.Service.Firewall;
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
        private readonly IFirewallService _firewall;

        public MatchmakerService(
            ILogger<MatchmakerService> logger,
            IOptions<ServerConfig> serverConfig,
            IOptions<HttpServerConfig> httpServerConfig,
            Matchmaker matchmaker,
            PortPoolService portPool,
            IFirewallService firewall)
        {
            _logger = logger;
            _serverConfig = serverConfig.Value;
            _httpServerConfig = httpServerConfig.Value;
            _matchmaker = matchmaker;
            _portPool = portPool;
            _firewall = firewall;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(_serverConfig.ResolveListenIp()), _serverConfig.ListenPort);

            if (_portPool.IsEnabled)
            {
                // Dynamic delta mode: no static UDP port is bound at startup.
                // Ports are allocated per player on demand; once the pool drops
                // to the low-water mark, new players are rejected.
                _matchmaker.Initialize(endpoint);
                _logger.LogInformation(
                    "Delta Matchmaker enabled: no static UDP port at startup (pool {Start}-{End}), ports created per player.",
                    _serverConfig.DeltaPortStart,
                    _serverConfig.DeltaPortEnd);
            }
            else
            {
                // Static mode: bind the main UDP listener on the configured port.
                await _matchmaker.StartAsync(endpoint);

                _logger.LogInformation(
                    "Matchmaker is listening on {Address}:{Port}, the public server ip is {PublicIp}:{PublicPort}.",
                    endpoint.Address,
                    endpoint.Port,
                    _serverConfig.ResolvePublicIp(),
                    _serverConfig.PublicPort);
            }

            // Open firewall for HTTP server TCP port
            if (_httpServerConfig.Enabled)
            {
                await _firewall.OpenPortAsync(_httpServerConfig.ListenPort, cancellationToken, "tcp");
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

using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Admin.Ban;

public sealed class BanEnforcementListener : IEventListener
{
    private readonly ILogger<BanEnforcementListener> _logger;
    private readonly BanStore _bans;

    public BanEnforcementListener(ILogger<BanEnforcementListener> logger, BanStore bans)
    {
        _logger = logger;
        _bans = bans;
    }

    [EventListener]
    public async ValueTask OnClientConnected(IClientConnectedEvent e)
    {
        var client = e.Client;
        var ip = client.Connection?.EndPoint?.Address;

        // IP 封禁检查
        if (ip != null && _bans.IsIpBanned(ip))
        {
            _logger.LogWarning("[Ban] Rejecting banned IP {Ip} ({Name})", ip, client.Name);
            await client.DisconnectAsync(DisconnectReason.Banned, "You are banned from this server.");
            return;
        }

        // FriendCode 封禁检查
        if (_bans.IsFriendCodeBanned(client.FriendCode))
        {
            _logger.LogWarning("[Ban] Rejecting banned FriendCode {FC} ({Name})", client.FriendCode, client.Name);
            await client.DisconnectAsync(DisconnectReason.Banned, "You are banned from this server.");
        }
    }
}

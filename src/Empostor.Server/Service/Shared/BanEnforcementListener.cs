using System;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Client;
using Empostor.Api.Localization;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Shared;

public sealed class BanEnforcementListener : IEventListener
{
    private readonly ILogger<BanEnforcementListener> _logger;
    private readonly BanStore _bans;
    private readonly ILocalizationService _language;

    public BanEnforcementListener(ILogger<BanEnforcementListener> logger, BanStore bans, ILocalizationService language)
    {
        _logger = logger;
        _bans = bans;
        _language = language;
    }

    [EventListener]
    public async ValueTask OnClientConnected(IClientConnectedEvent e)
    {
        var client = e.Client;
        var ip = client.Connection?.EndPoint?.Address;

        // IP ban check
        if (ip != null && _bans.GetIpBan(ip) is { } ipBan)
        {
            _logger.LogWarning("BanRejecting banned IP {Ip} ({Name})", ip, client.Name);
            await client.DisconnectAsync(DisconnectReason.Custom, BuildMessage(client.Language, ipBan.Reason, ipBan.BannedUntil));
            return;
        }

        // FriendCode ban check
        if (_bans.GetFriendCodeBan(client.FriendCode) is { } fcBan)
        {
            _logger.LogWarning("BanRejecting banned FriendCode {FC} ({Name})", client.FriendCode, client.Name);
            await client.DisconnectAsync(DisconnectReason.Custom, BuildMessage(client.Language, fcBan.Reason, fcBan.BannedUntil));
        }
    }

    private string BuildMessage(Language language, string reason, DateTime? bannedUntil)
    {
        var header = _language.Get(new LocalizedMessageKey("empostor", "ban.notice.header"), language);
        var reasonLine = _language.Get(new LocalizedMessageKey("empostor", "ban.notice.reason"), language, string.IsNullOrWhiteSpace(reason) ? "-" : reason);
        var unbanLine = bannedUntil.HasValue
            ? _language.Get(new LocalizedMessageKey("empostor", "ban.notice.unban"), language, FormatExpiry(bannedUntil.Value))
            : _language.Get(new LocalizedMessageKey("empostor", "ban.notice.unban_permanent"), language);
        var contactLine = _language.Get(new LocalizedMessageKey("empostor", "ban.notice.contact"), language);

        return $"{header}\n{reasonLine}\n{unbanLine}\n{contactLine}";
    }

    private static string FormatExpiry(DateTime utc)
        => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

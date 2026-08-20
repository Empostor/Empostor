using System;
using System.Threading.Tasks;
using Empostor.Api.Events;
using Empostor.Api.Events.Client;
using Empostor.Api.Languages;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Admin.Ban;

public sealed class BanEnforcementListener : IEventListener
{
    private readonly ILogger<BanEnforcementListener> _logger;
    private readonly BanStore _bans;
    private readonly LanguageService _language;

    public BanEnforcementListener(ILogger<BanEnforcementListener> logger, BanStore bans, LanguageService language)
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

        // IP 封禁检查
        if (ip != null && _bans.GetIpBan(ip) is { } ipBan)
        {
            _logger.LogWarning("BanRejecting banned IP {Ip} ({Name})", ip, client.Name);
            await client.DisconnectAsync(DisconnectReason.Custom, BuildMessage(client.Language, ipBan.Reason, ipBan.BannedUntil));
            return;
        }

        // FriendCode 封禁检查
        if (_bans.GetFriendCodeBan(client.FriendCode) is { } fcBan)
        {
            _logger.LogWarning("BanRejecting banned FriendCode {FC} ({Name})", client.FriendCode, client.Name);
            await client.DisconnectAsync(DisconnectReason.Custom, BuildMessage(client.Language, fcBan.Reason, fcBan.BannedUntil));
        }
    }

    private string BuildMessage(Language language, string reason, DateTime? bannedUntil)
    {
        var header = _language.Get("ban.notice.header", language).Get();
        var reasonLine = _language.Get("ban.notice.reason", language)
            .Format(string.IsNullOrWhiteSpace(reason) ? "-" : reason).Get();
        var unbanLine = bannedUntil.HasValue
            ? _language.Get("ban.notice.unban", language).Format(FormatExpiry(bannedUntil.Value)).Get()
            : _language.Get("ban.notice.unban_permanent", language).Get();
        var contactLine = _language.Get("ban.notice.contact", language).Get();

        return $"{header}\n{reasonLine}\n{unbanLine}\n{contactLine}";
    }

    private static string FormatExpiry(DateTime utc)
        => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

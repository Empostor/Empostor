using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

[EmpostorPlugin("cn.hayashiume.discordwebhook", "Discord Webhook", "HayashiUme", "2.0.0")]
public sealed class DiscordWebhookPlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<DiscordWebhookPlugin> _logger;

    public DiscordWebhookPlugin(ILogger<DiscordWebhookPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[DiscordWebhook] Enabled. Configure URLs in the admin panel (Plugins → Discord Webhook).");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["command.dcwb.description"] = "Turn this room's Discord posting on or off. Host only.",
                ["command.dcwb.usage"] = "dcwb <on|off>",
                ["discordwebhook.host_only"] = "Only the host can toggle Discord posting.",
                ["discordwebhook.usage"] = "Usage: #dcwb <on|off>",
                ["discordwebhook.already"] = "Discord posting is already {0} for this room.",
                ["discordwebhook.changed"] = "Discord posting {0} for this room.",
                ["discordwebhook.state_on"] = "on",
                ["discordwebhook.state_off"] = "off",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["command.dcwb.description"] = "开关本房间的 Discord 转发（仅房主）。",
                ["command.dcwb.usage"] = "dcwb <on|off>",
                ["discordwebhook.host_only"] = "只有房主可以开关 Discord 转发。",
                ["discordwebhook.usage"] = "用法：#dcwb <on|off>",
                ["discordwebhook.already"] = "本房间的 Discord 转发已经是{0}了。",
                ["discordwebhook.changed"] = "本房间的 Discord 转发已{0}。",
                ["discordwebhook.state_on"] = "开启",
                ["discordwebhook.state_off"] = "关闭",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["command.dcwb.description"] = "開關本房間的 Discord 轉發（僅房主）。",
                ["command.dcwb.usage"] = "dcwb <on|off>",
                ["discordwebhook.host_only"] = "只有房主可以開關 Discord 轉發。",
                ["discordwebhook.usage"] = "用法：#dcwb <on|off>",
                ["discordwebhook.already"] = "本房間的 Discord 轉發已經是{0}了。",
                ["discordwebhook.changed"] = "本房間的 Discord 轉發已{0}。",
                ["discordwebhook.state_on"] = "開啟",
                ["discordwebhook.state_off"] = "關閉",
            },
        };
    }
}

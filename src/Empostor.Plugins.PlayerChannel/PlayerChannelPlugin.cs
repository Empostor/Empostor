using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.PlayerChannel;

[EmpostorPlugin("cn.hayashiume.playerchannel", "Player Channel", "HayashiUme", "1.0.0")]
public sealed class PlayerChannelPlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<PlayerChannelPlugin> _logger;

    public PlayerChannelPlugin(ILogger<PlayerChannelPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[PlayerChannel] Enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["command.channel.description"] = "Send a message to your player channel.",
                ["command.channel.usage"] = "channel <message>",
                ["playerchannel.unknown_friendcode"] = "[Refuse Channel] Unknown Friendcode",
                ["playerchannel.not_in_channel"] = "[Refuse Channel] Not in any channel",
                ["playerchannel.message_format"] = "[{0}] {1}",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["command.channel.description"] = "向你的玩家频道发送消息。",
                ["command.channel.usage"] = "channel <消息>",
                ["playerchannel.unknown_friendcode"] = "[Refuse Channel] 未知的好友代码",
                ["playerchannel.not_in_channel"] = "[Refuse Channel] 不在任何频道中",
                ["playerchannel.message_format"] = "[{0}] {1}",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["command.channel.description"] = "向你的玩家頻道發送訊息。",
                ["command.channel.usage"] = "channel <訊息>",
                ["playerchannel.unknown_friendcode"] = "[Refuse Channel] 未知的好友代碼",
                ["playerchannel.not_in_channel"] = "[Refuse Channel] 不在任何頻道中",
                ["playerchannel.message_format"] = "[{0}] {1}",
            },
        };
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Message;

[EmpostorPlugin("cn.hayashiume.message")]
public sealed class MessagePlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<MessagePlugin> _logger;

    public MessagePlugin(ILogger<MessagePlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Message] Plugin enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["command.msg.description"] = "Leave a message for a player by friend code.",
                ["command.msg.usage"] = "msg <friendcode> <message>",
                ["message.usage"] = "Usage: #msg <friendcode> <message>\nExample: #msg ABCDEFG Hey, let's play again!",
                ["message.sent"] = "Message sent to {0}.",
                ["message.too_long"] = "Message is too long. Max {0} characters.",
                ["message.empty"] = "Message cannot be empty.",
                ["message.invalid_fc"] = "Invalid friend code format.",
                ["message.self"] = "You cannot send a message to yourself.",
                ["message.full"] = "{0} already has too many pending messages. Try again later.",
                ["message.delivery_header"] = "--- You have {0} pending message(s) ---",
                ["message.delivery_entry"] = "[{0}] <{1}> {2}: {3}",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["command.msg.description"] = "通过好友代码给玩家留言。",
                ["command.msg.usage"] = "msg <好友代码> <留言内容>",
                ["message.usage"] = "用法：#msg <好友代码> <留言内容>\n示例：#msg ABCDEFG 下次一起玩！",
                ["message.sent"] = "留言已发送给 {0}。",
                ["message.too_long"] = "留言内容过长，最多 {0} 个字符。",
                ["message.empty"] = "留言内容不能为空。",
                ["message.invalid_fc"] = "好友代码格式无效。",
                ["message.self"] = "不能给自己留言。",
                ["message.full"] = "{0} 的待收留言已满，请稍后再试。",
                ["message.delivery_header"] = "--- 你有 {0} 条待收留言 ---",
                ["message.delivery_entry"] = "[{0}] <{1}> {2}: {3}",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["command.msg.description"] = "透過好友代碼給玩家留言。",
                ["command.msg.usage"] = "msg <好友代碼> <留言內容>",
                ["message.usage"] = "用法：#msg <好友代碼> <留言內容>\n範例：#msg ABCDEFG 下次一起玩！",
                ["message.sent"] = "留言已發送給 {0}。",
                ["message.too_long"] = "留言內容過長，最多 {0} 個字元。",
                ["message.empty"] = "留言內容不能為空。",
                ["message.invalid_fc"] = "好友代碼格式無效。",
                ["message.self"] = "不能給自己留言。",
                ["message.full"] = "{0} 的待收留言已滿，請稍後再試。",
                ["message.delivery_header"] = "--- 你有 {0} 條待收留言 ---",
                ["message.delivery_entry"] = "[{0}] <{1}> {2}: {3}",
            },
        };
    }
}

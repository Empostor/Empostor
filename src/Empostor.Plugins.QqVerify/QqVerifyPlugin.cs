using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.QqVerify;

[EmpostorPlugin("cn.hayashiume.qqverify", "QQ Verify", "ELinmei", "1.0.0")]
public sealed class QqVerifyPlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<QqVerifyPlugin> _logger;

    public QqVerifyPlugin(ILogger<QqVerifyPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[QQVerify] Plugin enabled. #verify command registered.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["qqverify.chinese_only"] = "This command is only available for Chinese language players.\n此功能仅支持简体/繁体中文玩家使用。",
                ["qqverify.no_friendcode"] = "Unable to get your friend code. Please make sure you are logged in.",
                ["qqverify.usage_message"] = "Usage: #verify <your QQ number>\nExample: #verify 123456789",
                ["qqverify.recorded"] = "Verification request recorded! Send /验证 {0} to the QQ bot.\nNote: The code is valid for 10 minutes.",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["qqverify.chinese_only"] = "此功能仅支持简体/繁体中文玩家使用。\nThis command is only available for Chinese language players.",
                ["qqverify.no_friendcode"] = "无法获取你的好友代码，请确保已登录账号。",
                ["qqverify.usage_message"] = "用法：#verify <你的QQ号>\n示例：#verify 123456789",
                ["qqverify.recorded"] = "已记录验证请求！请私聊QQ机器人发送：/验证 {0}\n注意：验证码10分钟内有效。",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["qqverify.chinese_only"] = "此功能僅支援簡體/繁體中文玩家使用。\nThis command is only available for Chinese language players.",
                ["qqverify.no_friendcode"] = "無法獲取你的好友代碼，請確保已登入帳號。",
                ["qqverify.usage_message"] = "用法：#verify <你的QQ號>\n範例：#verify 123456789",
                ["qqverify.recorded"] = "已記錄驗證請求！請私聊QQ機器人發送：/驗證 {0}\n注意：驗證碼10分鐘內有效。",
            },
        };
    }
}

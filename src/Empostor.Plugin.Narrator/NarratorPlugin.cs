using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

[EmpostorPlugin("cn.hayashiume.narrator")]
public sealed class NarratorPlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<NarratorPlugin> _logger;

    public NarratorPlugin(ILogger<NarratorPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Narrator] Plugin enabled. #narrator command registered.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["command.narrator.description"] = "Ask the narrator for AI-powered advice during a meeting.",
                ["command.narrator.usage"] = "narrator <your question>",
                ["narrator.meeting_only"] = "You can only use #narrator during a meeting.",
                ["narrator.disabled"] = "The narrator is currently disabled by the host.",
                ["narrator.status_enabled"] = "enabled",
                ["narrator.status_disabled"] = "disabled",
                ["narrator.host_status"] = "Status: {0} | Max uses per player per game: {1}",
                ["narrator.host_commands"] = "Host commands: #narrator enable | disable | limit <number>",
                ["narrator.host_enabled"] = "Narrator enabled for this game.",
                ["narrator.host_disabled"] = "Narrator disabled for this game.",
                ["narrator.host_limit_set"] = "Narrator usage limit set to {0} per player per game.",
                ["narrator.host_limit_zero"] = "Narrator usage limit set to 0 (effectively disabled).",
                ["narrator.host_limit_usage"] = "Usage: #narrator limit <number>\nExample: #narrator limit 3",
                ["narrator.usage"] = "Usage: #narrator <your question or statement>\nExample: #narrator I'm being accused but I have a visual task, how do I prove myself?",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["command.narrator.description"] = "在会议中向旁白寻求AI建议。",
                ["command.narrator.usage"] = "narrator <你要提的问题>",
                ["narrator.meeting_only"] = "只能在会议中使用 #narrator。",
                ["narrator.disabled"] = "房主已禁用旁白功能。",
                ["narrator.status_enabled"] = "已启用",
                ["narrator.status_disabled"] = "已禁用",
                ["narrator.host_status"] = "状态：{0} | 每局每人最大次数：{1}",
                ["narrator.host_commands"] = "房主指令：#narrator enable | disable | limit <次数>",
                ["narrator.host_enabled"] = "旁白已在此局中启用。",
                ["narrator.host_disabled"] = "旁白已在此局中禁用。",
                ["narrator.host_limit_set"] = "旁白使用次数限制已设为每人每局 {0} 次。",
                ["narrator.host_limit_zero"] = "旁白使用次数限制已设为 0（等效禁用）。",
                ["narrator.host_limit_usage"] = "用法：#narrator limit <次数>\n示例：#narrator limit 3",
                ["narrator.usage"] = "用法：#narrator <你要说的话>\n示例：#narrator 我被指控了但我有可视任务，怎么证明自己？",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["command.narrator.description"] = "在會議中向旁白尋求AI建議。",
                ["command.narrator.usage"] = "narrator <你要提的問題>",
                ["narrator.meeting_only"] = "只能在會議中使用 #narrator。",
                ["narrator.disabled"] = "房主已停用旁白功能。",
                ["narrator.status_enabled"] = "已啟用",
                ["narrator.status_disabled"] = "已停用",
                ["narrator.host_status"] = "狀態：{0} | 每局每人最大次數：{1}",
                ["narrator.host_commands"] = "房主指令：#narrator enable | disable | limit <次數>",
                ["narrator.host_enabled"] = "旁白已在此局中啟用。",
                ["narrator.host_disabled"] = "旁白已在此局中停用。",
                ["narrator.host_limit_set"] = "旁白使用次數限制已設為每人每局 {0} 次。",
                ["narrator.host_limit_zero"] = "旁白使用次數限制已設為 0（等效停用）。",
                ["narrator.host_limit_usage"] = "用法：#narrator limit <次數>\n範例：#narrator limit 3",
                ["narrator.usage"] = "用法：#narrator <你要說的話>\n範例：#narrator 我被指控了但我有可視任務，怎麼證明自己？",
            },
        };
    }
}

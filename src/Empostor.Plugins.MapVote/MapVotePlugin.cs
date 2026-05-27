using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.MapVote;

[EmpostorPlugin("cn.hayashiume.mapvote")]
public sealed class MapVotePlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<MapVotePlugin> _logger;

    public MapVotePlugin(ILogger<MapVotePlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[MapVote] Plugin enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["command.votemap.description"] = "Vote for the next map.",
                ["command.votemap.usage"] = "votemap <map>",
                ["mapvote.disabled"] = "Map voting is currently disabled by the host.",
                ["mapvote.unknown_map"] = "Unknown map: {0}. Available: Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.voted"] = "{0} voted for {1}.",
                ["mapvote.already_voted"] = "You already voted for {0}. Use #votemap <map> to change your vote.",
                ["mapvote.no_votes"] = "No map votes yet. Use #votemap <map> to vote!",
                ["mapvote.results_header"] = "=== Map Vote Results ===",
                ["mapvote.results_entry"] = "  {0}: {1} vote(s)",
                ["mapvote.results_winner"] = "Winner: {0} ({1} votes)",
                ["mapvote.results_random"] = "No votes cast, using random map: {0}",
                ["mapvote.usage"] = "Usage: #votemap <map>\nMaps: Skeld, Mira, Polus, Airship, Fungle\nExample: #votemap polus",
                ["mapvote.host_commands"] = "Host commands: #votemap public | close | enable | disable | results",
                ["mapvote.host_only"] = "Only the host can use this command.",
                ["mapvote.host_enabled"] = "Map voting enabled.",
                ["mapvote.host_disabled"] = "Map voting disabled.",
                ["mapvote.session_started"] = "Map vote session started! Players, use #votemap <map> to vote.\nMaps: Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.map_set"] = "Map set to {0} by vote.",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["command.votemap.description"] = "投票选择下一张地图。",
                ["command.votemap.usage"] = "votemap <地图名>",
                ["mapvote.disabled"] = "投票选图功能已被房主禁用。",
                ["mapvote.unknown_map"] = "未知地图：{0}。可选：Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.voted"] = "{0} 投票选择了 {1}。",
                ["mapvote.already_voted"] = "你已经投票了 {0}。使用 #votemap <地图> 来修改投票。",
                ["mapvote.no_votes"] = "还没有人投票。使用 #votemap <地图> 来投票！",
                ["mapvote.results_header"] = "=== 地图投票结果 ===",
                ["mapvote.results_entry"] = "  {0}：{1} 票",
                ["mapvote.results_winner"] = "胜出：{0}（{1} 票）",
                ["mapvote.results_random"] = "无人投票，随机选择地图：{0}",
                ["mapvote.usage"] = "用法：#votemap <地图>\n可选地图：Skeld, Mira, Polus, Airship, Fungle\n示例：#votemap polus",
                ["mapvote.host_commands"] = "房主指令：#votemap public | close | enable | disable | results",
                ["mapvote.host_only"] = "只有房主可以使用此指令。",
                ["mapvote.host_enabled"] = "投票选图已启用。",
                ["mapvote.host_disabled"] = "投票选图已禁用。",
                ["mapvote.session_started"] = "地图投票已开始！请使用 #votemap <地图> 投票。\n可选地图：Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.map_set"] = "投票结果，地图已设为 {0}。",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["command.votemap.description"] = "投票選擇下一張地圖。",
                ["command.votemap.usage"] = "votemap <地圖名>",
                ["mapvote.disabled"] = "投票選圖功能已被房主停用。",
                ["mapvote.unknown_map"] = "未知地圖：{0}。可選：Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.voted"] = "{0} 投票選擇了 {1}。",
                ["mapvote.already_voted"] = "你已經投票了 {0}。使用 #votemap <地圖> 來修改投票。",
                ["mapvote.no_votes"] = "還沒有人投票。使用 #votemap <地圖> 來投票！",
                ["mapvote.results_header"] = "=== 地圖投票結果 ===",
                ["mapvote.results_entry"] = "  {0}：{1} 票",
                ["mapvote.results_winner"] = "勝出：{0}（{1} 票）",
                ["mapvote.results_random"] = "無人投票，隨機選擇地圖：{0}",
                ["mapvote.usage"] = "用法：#votemap <地圖>\n可選地圖：Skeld, Mira, Polus, Airship, Fungle\n範例：#votemap polus",
                ["mapvote.host_commands"] = "房主指令：#votemap public | close | enable | disable | results",
                ["mapvote.host_only"] = "只有房主可以使用此指令。",
                ["mapvote.host_enabled"] = "投票選圖已啟用。",
                ["mapvote.host_disabled"] = "投票選圖已停用。",
                ["mapvote.session_started"] = "地圖投票已開始！請使用 #votemap <地圖> 投票。\n可選地圖：Skeld, Mira, Polus, Airship, Fungle",
                ["mapvote.map_set"] = "投票結果，地圖已設為 {0}。",
            },
        };
    }
}

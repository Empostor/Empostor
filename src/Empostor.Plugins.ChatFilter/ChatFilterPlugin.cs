using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.ChatFilter;

[EmpostorPlugin("cn.hayashiume.chatfilter", "Chat Filter", "HayashiUme", "2.0.0")]
public sealed class ChatFilterPlugin : PluginBase
{
    private readonly ILogger<ChatFilterPlugin> _logger;

    public ChatFilterPlugin(ILogger<ChatFilterPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[ChatFilter] Enabled. Configure rules in the admin panel (Plugins → Chat Filter).");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

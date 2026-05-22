using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Chat;

[EmpostorPlugin("cn.hayashiume.chat")]
public sealed class ChatPlugin : PluginBase
{
    private readonly ILogger<ChatPlugin> _logger;

    public ChatPlugin(ILogger<ChatPlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Chat] Enabled — limiting message length and logging chat.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

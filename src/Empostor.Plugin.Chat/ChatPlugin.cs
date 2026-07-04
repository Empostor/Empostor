using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Empostor.Api.Utils;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Chat;

[EmpostorPlugin("cn.hayashiume.chat")]
public sealed class ChatPlugin : PluginBase
{
    private readonly ILogger<ChatPlugin> _logger;

    public ChatPlugin(ILogger<ChatPlugin> logger, IModuleTagRegistry tags)
    {
        _logger = logger;
        tags.Register("Empostor.Plugin.Chat", "Chat", "\x1b[92m");
    }

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("Chat plugin enabled — logging chat and limiting message length.");
        return default;
    }

    public override ValueTask DisableAsync() => default;
}

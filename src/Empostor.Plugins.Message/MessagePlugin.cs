using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Message;

[EmpostorPlugin(Owner)]
public sealed class MessagePlugin : PluginBase
{
    public const string Owner = "cn.hayashiume.message";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<MessagePlugin> _logger;
    private IDisposable? _registration;
    public MessagePlugin(ILogger<MessagePlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(MessagePlugin).Assembly, "Empostor.Plugins.Message.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

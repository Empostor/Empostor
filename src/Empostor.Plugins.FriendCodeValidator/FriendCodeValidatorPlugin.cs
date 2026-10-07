using System;
using System.Threading.Tasks;
using Empostor.Api.Localization;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FriendCodeValidator;

[EmpostorPlugin(Owner, "Friend Code Validator", "MatchDuck & Empostor", "1.0.0")]
public sealed class FriendCodeValidatorPlugin : PluginBase
{
    public const string Owner = "duck.Empostor.friendcodevalidator";
    private readonly ILocalizationRegistry _registry;
    private readonly ILogger<FriendCodeValidatorPlugin> _logger;
    private IDisposable? _registration;
    public FriendCodeValidatorPlugin(ILogger<FriendCodeValidatorPlugin> logger, ILocalizationRegistry registry) { _logger = logger; _registry = registry; }
    public override ValueTask EnableAsync()
    {
        if (!_registry.TryRegister(new LocalizationCatalog(Owner, typeof(FriendCodeValidatorPlugin).Assembly, "Empostor.Plugins.FriendCodeValidator.Localization"), out _registration))
            _logger.LogWarning("Could not register localization resources for {Owner}", Owner);
        return default;
    }
    public override ValueTask DisableAsync() { _registration?.Dispose(); return default; }
}

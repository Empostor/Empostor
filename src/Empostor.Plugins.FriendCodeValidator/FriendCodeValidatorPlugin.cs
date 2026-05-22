using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FriendCodeValidator
{
    // MatchDuck offered the Friendcode safenames.txt lol
    [EmpostorPlugin("duck.Empostor.friendcodevalidator", "Friend Code Validator", "MatchDuck & Empostor", "1.0.0")]
    public sealed class FriendCodeValidatorPlugin : PluginBase
    {
        private readonly ILogger<FriendCodeValidatorPlugin> _logger;

        public FriendCodeValidatorPlugin(ILogger<FriendCodeValidatorPlugin> logger)
            => _logger = logger;

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("[FriendCodeValidator] Enabled.");
            return default;
        }

        public override ValueTask DisableAsync() => default;
    }
}

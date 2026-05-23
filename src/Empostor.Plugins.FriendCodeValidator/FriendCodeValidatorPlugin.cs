using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FriendCodeValidator
{
    // MatchDuck offered the Friendcode safenames.txt lol
    [EmpostorPlugin("duck.Empostor.friendcodevalidator", "Friend Code Validator", "MatchDuck & Empostor", "1.0.0")]
    public sealed class FriendCodeValidatorPlugin : PluginBase, IPluginLanguageProvider
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

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string>
                {
                    ["friendcode.invalid_reason"] = "Your friend code contains inappropriate words and cannot be used on this server.",
                },
                ["zh_CN"] = new Dictionary<string, string>
                {
                    ["friendcode.invalid_reason"] = "您的好友代码包含不当词汇，无法在此服务器使用。",
                },
                ["zh_TW"] = new Dictionary<string, string>
                {
                    ["friendcode.invalid_reason"] = "您的好友代碼包含不當詞彙，無法在此伺服器使用。",
                },
            };
        }
    }
}

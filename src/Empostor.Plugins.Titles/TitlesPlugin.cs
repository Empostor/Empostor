using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Titles;

[EmpostorPlugin("cn.Empostor.titles", "Title System", "Empostor", "1.0.0")]
public sealed class TitlesPlugin : PluginBase, IPluginLanguageProvider
{
    private readonly ILogger<TitlesPlugin> _logger;

    public TitlesPlugin(ILogger<TitlesPlugin> logger) => _logger = logger;

    public override ValueTask EnableAsync()
    {
        _logger.LogInformation("[Titles] Plugin enabled.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetTranslations()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["titles.display_format"] = "[{0}] {1}",
            },
            ["zh_CN"] = new Dictionary<string, string>
            {
                ["titles.display_format"] = "[{0}] {1}",
            },
            ["zh_TW"] = new Dictionary<string, string>
            {
                ["titles.display_format"] = "[{0}] {1}",
            },
        };
    }
}

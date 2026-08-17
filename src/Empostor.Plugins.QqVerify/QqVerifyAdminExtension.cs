using Empostor.Api.Admin;
using Empostor.Api.Plugins;

namespace Empostor.Plugins.QqVerify;

public sealed class QqVerifyAdminExtension : IAdminExtension
{
    private readonly QqVerifyConfig _config;
    private readonly string _configPath;

    public QqVerifyAdminExtension(QqVerifyConfig config, string configPath)
    {
        _config = config;
        _configPath = configPath;
    }

    public string Id => "qq-verify";

    public string Title => "QQ Verify";

    public string Icon => "shield";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterTextbox(t =>
        {
            t.Label = "Bot secret";
            t.Value = _config.BotSecret;
            t.Secret = true;
            t.Placeholder = "change-bot-secret";
            t.OnSubmit(async ctx =>
            {
                _config.BotSecret = ctx.Value ?? string.Empty;
                PluginConfigLoader.Save(_configPath, _config);
                return AdminActionResult.Ok("Bot secret saved.");
            });
        });
    }
}

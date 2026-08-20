using Empostor.Api.Admin;
using Empostor.Api.Plugins;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorAdminExtension : IAdminExtension
{
    private readonly NarratorConfig _config;
    private readonly string _configPath;

    public NarratorAdminExtension(NarratorConfig config, string configPath)
    {
        _config = config;
        _configPath = configPath;
    }

    public string Id => "narrator";

    public string Title => "Narrator";

    public string Icon => "chat";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterTextbox(t =>
        {
            t.Label = "DeepSeek API key";
            t.Value = _config.ApiKey;
            t.Secret = true;
            t.Placeholder = "sk-...";
            t.OnSubmit(async ctx =>
            {
                _config.ApiKey = ctx.Value ?? string.Empty;
                PluginConfigLoader.Save(_configPath, _config);
                return AdminActionResult.Ok("API key saved.");
            });
        });

        b.RegisterTextbox(t =>
        {
            t.Label = "Model";
            t.Value = _config.Model;
            t.OnSubmit(async ctx =>
            {
                _config.Model = ctx.Value ?? string.Empty;
                PluginConfigLoader.Save(_configPath, _config);
                return AdminActionResult.Ok("Model saved.");
            });
        });

        b.RegisterTextbox(t =>
        {
            t.Label = "API endpoint";
            t.Value = _config.ApiEndpoint;
            t.OnSubmit(async ctx =>
            {
                _config.ApiEndpoint = ctx.Value ?? string.Empty;
                PluginConfigLoader.Save(_configPath, _config);
                return AdminActionResult.Ok("API endpoint saved.");
            });
        });

        b.RegisterNumber(n =>
        {
            n.Label = "Max uses per game";
            n.Value = _config.MaxUsesPerGame;
            n.Min = 0;
            n.OnChange(async ctx =>
            {
                if (int.TryParse(ctx.Value, out var v) && v >= 0)
                {
                    _config.MaxUsesPerGame = v;
                    PluginConfigLoader.Save(_configPath, _config);
                }

                return AdminActionResult.Ok("Max uses per game saved (applies to new games).");
            });
        });

        b.RegisterNumber(n =>
        {
            n.Label = "Max uses per meeting";
            n.Value = _config.MaxUsesPerMeeting;
            n.Min = 0;
            n.OnChange(async ctx =>
            {
                if (int.TryParse(ctx.Value, out var v) && v >= 0)
                {
                    _config.MaxUsesPerMeeting = v;
                    PluginConfigLoader.Save(_configPath, _config);
                }

                return AdminActionResult.Ok("Max uses per meeting saved (applies to new games).");
            });
        });
    }
}

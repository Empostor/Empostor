using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Empostor.Api.Admin;
using Empostor.Api.Plugins;

namespace Empostor.Plugins.PlayerChannel;

public sealed class PlayerChannelAdminExtension : IAdminExtension
{
    private readonly PlayerChannelConfig _config;
    private readonly string _configPath;

    public PlayerChannelAdminExtension(PlayerChannelConfig config, string configPath)
    {
        _config = config;
        _configPath = configPath;
    }

    public string Id => "player-channel";

    public string Title => "Player Channels";

    public string Icon => "link";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterTable(table =>
        {
            table.Columns = new List<string> { "Channel", "Friend Codes" };
            table.Rows = _config.Channels.Select(c => new List<AdminTableCell>
            {
                new() { Text = c.Name },
                new() { Text = string.Join(", ", c.FriendCodes), Monospace = true },
            }).ToList();
        });

        b.RegisterTextbox(t =>
        {
            t.Label = "Edit channels (JSON)";
            t.Multiline = true;
            t.Value = JsonSerializer.Serialize(_config.Channels, new JsonSerializerOptions { WriteIndented = true });
            t.OnSubmit(async ctx =>
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<ChannelEntry>>(ctx.Value ?? "[]");
                    if (list == null)
                    {
                        return AdminActionResult.Fail("Invalid JSON.");
                    }

                    _config.Channels = list;
                    PluginConfigLoader.Save(_configPath, _config);
                    return AdminActionResult.Ok($"Saved {list.Count} channel(s).");
                }
                catch (JsonException ex)
                {
                    return AdminActionResult.Fail("Invalid JSON: " + ex.Message);
                }
            });
        });
    }
}

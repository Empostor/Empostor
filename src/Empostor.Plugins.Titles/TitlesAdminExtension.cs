using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Empostor.Api.Admin;
using Empostor.Api.Plugins;
using Empostor.Plugins.Titles.Service;

namespace Empostor.Plugins.Titles;

public sealed class TitlesAdminExtension : IAdminExtension
{
    private readonly TitlesConfig _config;
    private readonly string _configPath;
    private readonly FriendCodeTitleListener _listener;

    public TitlesAdminExtension(TitlesConfig config, string configPath, FriendCodeTitleListener listener)
    {
        _config = config;
        _configPath = configPath;
        _listener = listener;
    }

    public string Id => "titles";

    public string Title => "Title System";

    public string Icon => "list";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterTable(table =>
        {
            table.Columns = new List<string> { "Friend Code", "Title" };
            table.Rows = _config.Titles.Select(t => new List<AdminTableCell>
            {
                new() { Text = t.FriendCode, Monospace = true },
                new() { Text = t.Title },
            }).ToList();
        });

        b.RegisterTextbox(t =>
        {
            t.Label = "Edit titles (JSON)";
            t.Multiline = true;
            t.Value = JsonSerializer.Serialize(_config.Titles, new JsonSerializerOptions { WriteIndented = true });
            t.OnSubmit(async ctx =>
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<FriendCodeTitle>>(ctx.Value ?? "[]");
                    if (list == null)
                    {
                        return AdminActionResult.Fail("Invalid JSON.");
                    }

                    _config.Titles = list;
                    PluginConfigLoader.Save(_configPath, _config);
                    _listener.Reload();
                    return AdminActionResult.Ok($"Saved {list.Count} title(s).");
                }
                catch (JsonException ex)
                {
                    return AdminActionResult.Fail("Invalid JSON: " + ex.Message);
                }
            });
        });
    }
}

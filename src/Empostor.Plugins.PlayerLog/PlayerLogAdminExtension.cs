using System.Linq;
using Empostor.Api.Admin;

namespace Empostor.Plugins.PlayerLog;

public sealed class PlayerLogAdminExtension : IAdminExtension
{
    private readonly PlayerLogStore _store;

    public PlayerLogAdminExtension(PlayerLogStore store)
    {
        _store = store;
    }

    public string Id => "player-log";

    public string Title => "Player Log";

    public string Icon => "list";

    public string Section => "Server";

    public void Build(AdminPanelBuilder b)
    {
        var recent = _store.GetAll().Take(50).ToList();

        b.RegisterTable(t =>
        {
            t.Columns = new() { "Time", "Type", "Player", "Friend Code", "Game", "Detail" };
            foreach (var e in recent)
            {
                t.Rows.Add(new()
                {
                    new() { Text = e.Time.ToString("MM-dd HH:mm:ss") },
                    new() { Text = e.Type },
                    new() { Text = e.PlayerName ?? "—" },
                    new() { Text = e.FriendCode ?? "—" },
                    new() { Text = e.GameCode ?? "—" },
                    new() { Text = e.Detail ?? "—" },
                });
            }
        });

        b.RegisterText(txt =>
        {
            txt.Content = $"Total entries: {_store.GetAll().Count}";
            txt.Tone = "muted";
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Export as JSON";
            btn.Icon = "download";
            btn.Style = "secondary";
            btn.OnClick(async ctx =>
            {
                var data = _store.ExportJson();
                return AdminActionResult.Ok($"Exported {data.Length} bytes. Download via /api/admin/player/logs/export");
            });
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Clear All Logs";
            btn.Icon = "trash";
            btn.Style = "danger";
            btn.OnClick(async ctx =>
            {
                _store.Clear();
                return AdminActionResult.Ok("All player logs cleared.");
            });
        });
    }
}

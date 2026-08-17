using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Admin;

namespace Empostor.Plugins.PlayerStats;

public sealed class PlayerStatsAdminExtension : IAdminExtension
{
    private readonly PlayerStatsStore _store;

    public PlayerStatsAdminExtension(PlayerStatsStore store)
    {
        _store = store;
    }

    public string Id => "player-stats";

    public string Title => "Player Stats";

    public string Icon => "stats";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterToggle(t =>
        {
            t.Label = "Enable stat recording";
            t.Value = _store.Enabled;
            t.OnChange(async ctx =>
            {
                _store.SetEnabled(ctx.Value == "true");
                return AdminActionResult.Ok("Stat recording " + (_store.Enabled ? "enabled." : "disabled."));
            });
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Reset all stats";
            btn.Icon = "trash";
            btn.Style = "danger";
            btn.OnClick(async ctx =>
            {
                _store.ClearAll();
                return AdminActionResult.Ok("All statistics reset.");
            });
        });

        b.RegisterTable(table =>
        {
            table.Columns = new List<string>
            {
                "Friend Code", "Name", "Games", "Wins", "Losses", "Imp. Wins", "Kills", "Deaths", "Tasks", "Exiled",
            };
            table.Rows = _store.GetAll().Select(s => new List<AdminTableCell>
            {
                new() { Text = s.FriendCode, Monospace = true },
                new() { Text = s.LastKnownName ?? "—" },
                new() { Text = s.GamesPlayed.ToString() },
                new() { Text = s.Wins.ToString() },
                new() { Text = s.Losses.ToString() },
                new() { Text = s.ImpostorWins.ToString() },
                new() { Text = s.Kills.ToString() },
                new() { Text = s.Deaths.ToString() },
                new() { Text = s.TasksCompleted.ToString() },
                new() { Text = s.TimesExiled.ToString() },
            }).ToList();
        });
    }
}

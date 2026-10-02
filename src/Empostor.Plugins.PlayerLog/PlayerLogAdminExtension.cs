using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empostor.Api.Admin;

namespace Empostor.Plugins.PlayerLog;

/// <summary>
///     Admin panel page for the player log. The selected player, page and clear range live on this
///     singleton, so every rebuild of the panel reflects the last choice.
/// </summary>
public sealed class PlayerLogAdminExtension : IAdminExtension
{
    private const int PageSize = 100;

    private static readonly string[] TypeColors = { "Chat", "Report", "Murder", "Exile", "Vote", "Task", "Vent", "Meeting" };

    private readonly PlayerLogStore _store;

    private int? _clientId;
    private int _page = 1;
    private string _clearRange = "all";

    public PlayerLogAdminExtension(PlayerLogStore store)
    {
        _store = store;
    }

    public string Id => "player-log";

    public string Title => "Player Log";

    public string Icon => "list";

    public string Section => "Server";

    /// <summary>The chat feed mirrors live server state, so keep the 1-second rebuild.</summary>
    public bool AutoRefresh => true;

    public void Build(AdminPanelBuilder b)
    {
        var players = _store.GetLoggedClientIds();
        if (_clientId.HasValue && !players.Contains(_clientId.Value))
        {
            _clientId = null;
        }

        var total = _clientId.HasValue ? _store.GetCountByClient(_clientId.Value) : _store.GetCount();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        _page = Math.Clamp(_page, 1, totalPages);

        var entries = _clientId.HasValue
            ? _store.GetPageByClient(_clientId.Value, _page, PageSize)
            : _store.GetPage(_page, PageSize);

        b.RegisterSelect(sel =>
        {
            sel.Label = "Player";
            sel.Options = new List<AdminOption> { new("All players", string.Empty) };
            foreach (var id in players)
            {
                var name = _store.GetLatestName(id) ?? $"Player#{id}";
                var fc = _store.GetLatestFriendCode(id) ?? "—";
                sel.Options.Add(new AdminOption($"#{id} {name} ({fc})", id.ToString()));
            }

            sel.Value = _clientId?.ToString() ?? string.Empty;
            sel.OnChange(ctx =>
            {
                _clientId = int.TryParse(ctx.Value, out var id) ? id : null;
                _page = 1;
                return ValueTask.FromResult(AdminActionResult.Ok(null));
            });
        });

        b.RegisterSelect(sel =>
        {
            sel.Label = "Page";
            sel.Options = Enumerable.Range(1, totalPages)
                .Select(p => new AdminOption($"{p} / {totalPages}", p.ToString()))
                .ToList();
            sel.Value = _page.ToString();
            sel.OnChange(ctx =>
            {
                if (int.TryParse(ctx.Value, out var page))
                {
                    _page = page;
                }

                return ValueTask.FromResult(AdminActionResult.Ok(null));
            });
        });

        b.RegisterTable(t =>
        {
            t.Columns = new List<string> { "Time", "Type", "Player", "Friend Code", "Game", "Detail" };
            foreach (var entry in entries)
            {
                t.Rows.Add(new List<AdminTableCell>
                {
                    new() { Text = entry.Time.ToLocalTime().ToString("MM-dd HH:mm:ss") },
                    new() { Text = entry.Type, Tone = TypeColors.Contains(entry.Type) ? "accent" : "default" },
                    new() { Text = entry.PlayerName ?? "—" },
                    new() { Text = entry.FriendCode ?? "—", Monospace = true },
                    new() { Text = entry.GameCode ?? "—", Monospace = true },
                    new() { Text = entry.Detail ?? "—" },
                });
            }
        });

        b.RegisterText(txt =>
        {
            txt.Content = total == 0
                ? "No log entries recorded yet."
                : $"Showing {entries.Count} of {total} entr{(total == 1 ? "y" : "ies")} · export: /player-log/export";
            txt.Tone = "muted";
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Export as JSON";
            btn.Icon = "download";
            btn.Style = "secondary";
            btn.OnClick(ctx =>
            {
                var bytes = _clientId.HasValue ? _store.ExportJson(_clientId.Value) : _store.ExportJson();
                var scope = _clientId.HasValue ? $"client {_clientId}" : "all players";
                return ValueTask.FromResult(AdminActionResult.Ok(
                    $"{bytes.Length} bytes ready for {scope}. Open /player-log/export to download."));
            });
        });

        b.RegisterSelect(sel =>
        {
            sel.Label = "Clear logs";
            sel.Options = new List<AdminOption>
            {
                new("All logs", "all"),
                new("Older than 1 hour", "1h"),
                new("Older than 24 hours", "24h"),
                new("Older than 7 days", "7d"),
                new("Older than 30 days", "30d"),
            };
            sel.Value = _clearRange;
            sel.OnChange(ctx =>
            {
                _clearRange = string.IsNullOrWhiteSpace(ctx.Value) ? "all" : ctx.Value!;
                return ValueTask.FromResult(AdminActionResult.Ok(null));
            });
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Clear Logs";
            btn.Icon = "trash";
            btn.Style = "danger";
            btn.OnClick(ctx =>
            {
                var cutoff = _clearRange switch
                {
                    "1h" => DateTime.UtcNow.AddHours(-1),
                    "24h" => DateTime.UtcNow.AddDays(-1),
                    "7d" => DateTime.UtcNow.AddDays(-7),
                    "30d" => DateTime.UtcNow.AddDays(-30),
                    _ => (DateTime?)null,
                };

                _store.Clear(cutoff);
                _page = 1;
                var scope = cutoff.HasValue ? $"older than {_clearRange}" : "all";
                return ValueTask.FromResult(AdminActionResult.Ok($"Cleared {scope} log entries."));
            });
        });
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Empostor.Api.Admin;

namespace Empostor.Plugin.Chat;

public sealed class ChatAdminExtension : IAdminExtension
{
    private const int MessagePreviewLength = 220;

    private readonly ChatStore _store;
    private readonly ChatConfig _config;

    private string _room = string.Empty;
    private bool _paused;
    private List<ChatEntry> _frozen = new();

    public ChatAdminExtension(ChatStore store)
    {
        _store = store;
        _config = ChatConfig.Load();
    }

    public string Id => "chat-monitor";

    public string Title => "Chat Monitor";

    public string Icon => "chat";

    public string Section => "Server";

    public void Build(AdminPanelBuilder b)
    {
        var visible = Math.Max(10, _config.MaxVisibleMessages);
        var entries = _paused ? _frozen : _store.Recent(_room, visible);

        b.RegisterSelect(sel =>
        {
            sel.Label = "Room";
            sel.Options = new List<AdminOption> { new("All rooms", string.Empty) };
            foreach (var room in _store.Rooms())
            {
                sel.Options.Add(new AdminOption(room, room));
            }

            sel.Value = _room;
            sel.OnChange(ctx =>
            {
                _room = ctx.Value ?? string.Empty;
                if (_paused)
                {
                    _frozen = _store.Recent(_room, visible);
                }

                return ValueTask.FromResult(AdminActionResult.Ok(null));
            });
        });

        b.RegisterToggle(t =>
        {
            t.Label = "Pause auto-refresh (the list stops moving while you read)";
            t.Value = _paused;
            t.OnChange(ctx =>
            {
                var pause = string.Equals(ctx.Value, "true", StringComparison.OrdinalIgnoreCase);
                if (pause && !_paused)
                {
                    _frozen = _store.Recent(_room, visible);
                }

                _paused = pause;
                return ValueTask.FromResult(AdminActionResult.Ok(
                    pause ? "Paused. Press Refresh to pull the latest lines." : "Resumed — updating every second."));
            });
        });

        b.RegisterTable(t =>
        {
            t.Columns = new List<string> { "Time", "Room", "Player", "Channel", "Message" };
            foreach (var entry in entries)
            {
                t.Rows.Add(new List<AdminTableCell>
                {
                    new() { Text = entry.Time.ToLocalTime().ToString("HH:mm:ss"), Monospace = true },
                    new() { Text = entry.GameCode, Monospace = true, Tone = "accent" },
                    new()
                    {
                        Text = string.IsNullOrEmpty(entry.FriendCode)
                            ? entry.PlayerName
                            : $"{entry.PlayerName} ({entry.FriendCode})",
                    },
                    new() { Text = entry.Channel, Tone = entry.Blocked ? "danger" : "muted" },
                    new() { Text = Preview(entry.Message) },
                });
            }
        });

        b.RegisterText(txt =>
        {
            txt.Tone = "muted";
            txt.Content = _store.Count == 0
                ? "No chat captured yet. Lines appear here as players talk in game."
                : $"Showing {entries.Count} of {_store.Count} buffered line(s), buffer limit {_store.Capacity}"
                  + (_paused ? " · PAUSED" : " · live");
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Refresh";
            btn.Icon = "refresh";
            btn.Style = "secondary";
            btn.OnClick(ctx =>
            {
                if (!_paused)
                {
                    return ValueTask.FromResult(AdminActionResult.Ok("Live view — already updating every second."));
                }

                _frozen = _store.Recent(_room, visible);
                return ValueTask.FromResult(AdminActionResult.Ok("Snapshot refreshed."));
            });
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Clear Buffer";
            btn.Icon = "trash";
            btn.Style = "danger";
            btn.OnClick(ctx =>
            {
                _store.Clear();
                _frozen = new List<ChatEntry>();
                return ValueTask.FromResult(AdminActionResult.Ok("Chat buffer cleared."));
            });
        });
    }

    /// <summary>Keeps long or multi-line messages from blowing up the table layout.</summary>
    private static string Preview(string message)
    {
        var flat = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length > MessagePreviewLength ? flat.Substring(0, MessagePreviewLength) + "…" : flat;
    }
}

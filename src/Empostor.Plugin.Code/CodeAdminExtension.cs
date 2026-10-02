using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Admin;
using Empostor.Api.Games;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Code;

/// <summary>
///     Admin panel page for the room-code pool: a searchable, editable list of every code
///     (available and in use), an add row, and a reload button for Boot.Codes word lists.
/// </summary>
public sealed class CodeAdminExtension : IAdminExtension
{
    private readonly IGameCodeManager _manager;
    private readonly ILogger<CodeAdminExtension> _logger;

    public CodeAdminExtension(IGameCodeManager manager, ILogger<CodeAdminExtension> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public string Id => "game-codes";

    public string Title => "Game Codes";

    public string Icon => "database";

    public void Build(AdminPanelBuilder b)
    {
        var entries = _manager.Entries;
        var inUse = entries.Count(e => e.InUse);

        b.RegisterEntries(ent =>
        {
            ent.Fields = new List<AdminEntryField>
            {
                new() { Key = "code", Label = "Room Code", Monospace = true },
            };
            ent.Rows = entries
                .Select(e => new Dictionary<string, string> { ["code"] = e.Code.Code })
                .ToList();
            ent.SearchPlaceholder = $"Search {entries.Count} code(s)...";
            ent.AddLabel = "Add code";
            ent.OnAdd(OnAddEntry);
            ent.OnEdit(OnEditEntry);
            ent.OnRemove(OnRemoveEntry);
        });

        b.RegisterText(t =>
        {
            t.Tone = "muted";
            t.Content = $"{_manager.FourCharCodes} four-char + {_manager.SixCharCodes} six-char code(s) loaded "
                + $"({entries.Count} total: {entries.Count - inUse} available, {inUse} in use). "
                + $"Source directory: {_manager.Path} — codes added here are also saved to 00-admin-added.txt.";
        });

        b.RegisterButton(btn =>
        {
            btn.Label = "Reload from Boot.Codes";
            btn.Icon = "refresh";
            btn.Style = "secondary";
            btn.OnClick(ctx =>
            {
                var count = _manager.Reload();
                return ValueTask.FromResult(AdminActionResult.Ok(
                    $"{count} code(s) loaded from Boot.Codes (codes in use were kept)."));
            });
        });
    }

    private ValueTask<AdminActionResult> OnAddEntry(AdminActionContext ctx)
    {
        var code = ReadCode(ctx);
        if (code == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Enter a room code (4 or 6 letters)."));
        }

        var (ok, message) = _manager.Add(code);
        return ValueTask.FromResult(ok ? AdminActionResult.Ok(message) : AdminActionResult.Fail(message));
    }

    /// <summary>Editing a code = add the replacement, then drop the old one.</summary>
    private ValueTask<AdminActionResult> OnEditEntry(AdminActionContext ctx)
    {
        var newCode = ReadCode(ctx);
        if (newCode == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Enter a room code (4 or 6 letters)."));
        }

        var oldText = ctx.Value?.Trim().ToUpperInvariant();
        if (string.Equals(oldText, newCode, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(AdminActionResult.Ok("Code unchanged."));
        }

        var (addOk, addMessage) = _manager.Add(newCode);
        if (!addOk)
        {
            return ValueTask.FromResult(AdminActionResult.Fail(addMessage));
        }

        var note = string.Empty;
        if (!string.IsNullOrEmpty(oldText))
        {
            var oldCode = new GameCode(oldText);
            if (!oldCode.IsInvalid)
            {
                var (_, removeMessage) = _manager.Remove(oldCode);
                note = removeMessage;
            }
        }

        return ValueTask.FromResult(AdminActionResult.Ok($"Code changed to {newCode}." + note));
    }

    private ValueTask<AdminActionResult> OnRemoveEntry(AdminActionContext ctx)
    {
        var text = ctx.Value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Missing room code."));
        }

        var code = new GameCode(text);
        if (code.IsInvalid)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Not a valid room code."));
        }

        var (ok, message) = _manager.Remove(code);
        return ValueTask.FromResult(ok ? AdminActionResult.Ok(message) : AdminActionResult.Fail(message));
    }

    private static string? ReadCode(AdminActionContext ctx)
    {
        if (ctx.Payload == null || ctx.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!ctx.Payload.Value.TryGetProperty("row", out var rowEl)
            || rowEl.ValueKind != JsonValueKind.Object
            || !rowEl.TryGetProperty("code", out var codeEl)
            || codeEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var code = codeEl.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Admin;
using Empostor.Api.Games;
using Empostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FixedCode;

/// <summary>
///     Admin panel page to manage the friend-code → room-code mappings: searchable table with
///     per-row edit/delete and an add row at the bottom. Every change rebuilds the listener's
///     in-memory map immediately, so edits take effect without a restart.
/// </summary>
public sealed class FixedCodeAdminExtension : IAdminExtension
{
    private readonly FixedCodeStore _store;
    private readonly FixedCodeListener _listener;
    private readonly ILogger<FixedCodeAdminExtension> _logger;

    public FixedCodeAdminExtension(
        FixedCodeStore store,
        FixedCodeListener listener,
        ILogger<FixedCodeAdminExtension> logger)
    {
        _store = store;
        _listener = listener;
        _logger = logger;
    }

    public string Id => "fixed-code";

    public string Title => "Fixed Room Code";

    public string Icon => "link";

    public string Section => "Plugins";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterEntries(ent =>
        {
            ent.Fields = new List<AdminEntryField>
            {
                new() { Key = "friendCode", Label = "Friend Code", Monospace = true },
                new() { Key = "roomCode", Label = "Room Code", Monospace = true },
            };
            ent.Rows = _store.Mappings
                .Select(m => new Dictionary<string, string>
                {
                    ["friendCode"] = m.FriendCode,
                    ["roomCode"] = m.RoomCode,
                })
                .ToList();
            ent.SearchPlaceholder = "Search friend code or room code...";
            ent.AddLabel = "Add mapping";
            ent.OnAdd(OnAddEntry);
            ent.OnEdit(OnEditEntry);
            ent.OnRemove(OnRemoveEntry);
        });

        b.RegisterText(t =>
        {
            t.Tone = "muted";
            t.Content = "Hosts whose friend code is listed always get the mapped room code when creating a lobby. "
                + $"Currently {_listener.MappingCount} active mapping(s). Changes apply immediately, no restart needed.";
        });
    }

    private ValueTask<AdminActionResult> OnAddEntry(AdminActionContext ctx)
    {
        var row = ReadRow(ctx);
        if (row == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Friend code and room code are both required."));
        }

        var friendCode = row["friendCode"];
        var roomCode = row["roomCode"].Trim().ToUpperInvariant();

        var error = ValidateRoomCode(roomCode);
        if (error != null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail(error));
        }

        if (_store.Find(friendCode) != null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail($"{friendCode} already has a mapping — edit it instead."));
        }

        var mappings = _store.Mappings.ToList();
        mappings.Add(new FriendCodeMapping { FriendCode = friendCode, RoomCode = roomCode });
        _store.SetMappings(mappings);
        _listener.Rebuild();

        _logger.LogInformation("[FixedCode] Mapping {FC} → {Code} added via admin panel", friendCode, roomCode);
        return ValueTask.FromResult(AdminActionResult.Ok($"Mapping {friendCode} → {roomCode} added."));
    }

    private ValueTask<AdminActionResult> OnEditEntry(AdminActionContext ctx)
    {
        var oldFriendCode = ctx.Value;
        var row = ReadRow(ctx);
        if (string.IsNullOrWhiteSpace(oldFriendCode) || row == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("Friend code and room code are both required."));
        }

        var existing = _store.Find(oldFriendCode);
        if (existing == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("That mapping no longer exists — refresh and try again."));
        }

        var friendCode = row["friendCode"];
        var roomCode = row["roomCode"].Trim().ToUpperInvariant();

        var error = ValidateRoomCode(roomCode);
        if (error != null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail(error));
        }

        if (!string.Equals(friendCode, oldFriendCode, StringComparison.OrdinalIgnoreCase)
            && _store.Find(friendCode) != null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail($"{friendCode} already has a mapping."));
        }

        existing.FriendCode = friendCode;
        existing.RoomCode = roomCode;
        _store.SetMappings(_store.Mappings.ToList());
        _listener.Rebuild();

        _logger.LogInformation("[FixedCode] Mapping {Old} → {FC} → {Code} edited via admin panel", oldFriendCode, friendCode, roomCode);
        return ValueTask.FromResult(AdminActionResult.Ok($"Mapping updated to {friendCode} → {roomCode}."));
    }

    private ValueTask<AdminActionResult> OnRemoveEntry(AdminActionContext ctx)
    {
        var friendCode = ctx.Value;
        var existing = _store.Find(friendCode);
        if (existing == null)
        {
            return ValueTask.FromResult(AdminActionResult.Fail("That mapping no longer exists — refresh and try again."));
        }

        _store.SetMappings(_store.Mappings
            .Where(m => !string.Equals(m.FriendCode, friendCode, StringComparison.OrdinalIgnoreCase))
            .ToList());
        _listener.Rebuild();

        _logger.LogInformation("[FixedCode] Mapping {FC} → {Code} removed via admin panel", friendCode, existing.RoomCode);
        return ValueTask.FromResult(AdminActionResult.Ok($"Mapping {friendCode} → {existing.RoomCode} removed."));
    }

    /// <summary>The edit/remove actions identify the row by its first field (friend code).</summary>
    private static Dictionary<string, string>? ReadRow(AdminActionContext ctx)
    {
        if (ctx.Payload == null || ctx.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!ctx.Payload.Value.TryGetProperty("row", out var rowEl)
            || rowEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in rowEl.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        if (!result.TryGetValue("friendCode", out var friendCode) || string.IsNullOrWhiteSpace(friendCode))
        {
            return null;
        }

        if (!result.TryGetValue("roomCode", out var roomCode) || string.IsNullOrWhiteSpace(roomCode))
        {
            return null;
        }

        return result;
    }

    private static string? ValidateRoomCode(string roomCode)
    {
        if (roomCode.Length != 4 && roomCode.Length != 6)
        {
            return "Room code must be 4 or 6 letters.";
        }

        if (!roomCode.All(char.IsLetter))
        {
            return "Room code may only contain letters.";
        }

        return new GameCode(roomCode).IsInvalid ? "Not a valid room code." : null;
    }
}

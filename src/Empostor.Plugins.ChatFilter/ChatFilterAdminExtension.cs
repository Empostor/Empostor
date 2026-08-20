using System.Linq;
using Empostor.Api.Admin;

namespace Empostor.Plugins.ChatFilter;

public sealed class ChatFilterAdminExtension : IAdminExtension
{
    private readonly ChatFilterStore _store;

    public ChatFilterAdminExtension(ChatFilterStore store)
    {
        _store = store;
    }

    public string Id => "chat-filter";

    public string Title => "Chat Filter";

    public string Icon => "filter";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterToggle(t =>
        {
            t.Label = "Enable filtering";
            t.Value = _store.Enabled;
            t.OnChange(async ctx =>
            {
                _store.Enabled = ctx.Value == "true";
                await _store.SaveAsync();
                return AdminActionResult.Ok("Chat filter " + (_store.Enabled ? "enabled." : "disabled."));
            });
        });

        b.RegisterToggle(t =>
        {
            t.Label = "Block messages (off = log only)";
            t.Value = _store.BlockMessage;
            t.OnChange(async ctx =>
            {
                _store.BlockMessage = ctx.Value == "true";
                await _store.SaveAsync();
                return AdminActionResult.Ok("Saved.");
            });
        });

        b.RegisterNumber(n =>
        {
            n.Label = "Spam threshold (messages)";
            n.Value = _store.SpamThreshold;
            n.Min = 0;
            n.OnChange(async ctx =>
            {
                if (int.TryParse(ctx.Value, out var v))
                {
                    _store.SpamThreshold = v;
                    await _store.SaveAsync();
                }

                return AdminActionResult.Ok("Spam threshold saved.");
            });
        });

        b.RegisterNumber(n =>
        {
            n.Label = "Spam window (seconds)";
            n.Value = _store.SpamWindowSeconds;
            n.Min = 1;
            n.OnChange(async ctx =>
            {
                if (int.TryParse(ctx.Value, out var v) && v > 0)
                {
                    _store.SpamWindowSeconds = v;
                    await _store.SaveAsync();
                }

                return AdminActionResult.Ok("Spam window saved.");
            });
        });

        b.RegisterChips(c =>
        {
            c.Placeholder = "Add a blocked word…";
            c.AddLabel = "Add";
            c.Items = _store.BlockedWords.Select(w => new AdminChip(w, w)).ToList();
            c.OnAdd(async ctx =>
            {
                _store.AddWord(ctx.Value ?? string.Empty);
                return AdminActionResult.Ok("Word added.");
            });
            c.OnRemove(async ctx =>
            {
                _store.RemoveWord(ctx.Value ?? string.Empty);
                return AdminActionResult.Ok("Word removed.");
            });
        });
    }
}

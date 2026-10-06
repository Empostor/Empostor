using System;
using System.Threading.Tasks;
using Empostor.Api.Admin;

namespace Empostor.Plugins.Maintenance;

public sealed class MaintenanceAdminExtension : IAdminExtension
{
    private readonly MaintenanceStore _store;
    private readonly MaintenanceListener _listener;

    public MaintenanceAdminExtension(MaintenanceStore store, MaintenanceListener listener)
    {
        _store = store;
        _listener = listener;
    }

    public string Id => "maintenance";

    public string Title => "Maintenance";

    public string Icon => "shield";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterText(
            _store.Enabled
                ? "Maintenance mode: ON — every player is turned away."
                : "Maintenance mode: OFF — the server is accepting players.",
            _store.Enabled ? "warning" : "default");

        b.RegisterButton(button =>
        {
            button.Label = _store.Enabled ? "Turn maintenance OFF" : "Turn maintenance ON";
            button.Icon = _store.Enabled ? "refresh" : "shield";
            button.Style = _store.Enabled ? "primary" : "danger";
            button.OnClick(async ctx => await ToggleAsync(ctx));
        });

        b.RegisterDivider();

        b.RegisterTextbox(textbox =>
        {
            textbox.Label = "Kick message (Chinese)";
            textbox.Value = _store.MessageZh;
            textbox.Multiline = true;
            textbox.OnSubmit(async ctx => await SaveMessageAsync(ctx, zh: ctx.Value, en: null));
        });

        b.RegisterTextbox(textbox =>
        {
            textbox.Label = "Kick message (English)";
            textbox.Value = _store.MessageEn;
            textbox.Multiline = true;
            textbox.OnSubmit(async ctx => await SaveMessageAsync(ctx, zh: null, en: ctx.Value));
        });
    }

    private async ValueTask<AdminActionResult> ToggleAsync(AdminActionContext ctx)
    {
        var enabled = !_store.Enabled;
        await _listener.SetAsync(enabled);

        return AdminActionResult.Ok(enabled
            ? "Maintenance mode ON. Everyone in a lobby was disconnected; new players are refused."
            : "Maintenance mode OFF. The server is accepting players again.");
    }

    private async ValueTask<AdminActionResult> SaveMessageAsync(AdminActionContext ctx, string? zh, string? en)
    {
        _store.SetMessages(
            string.IsNullOrWhiteSpace(zh) ? _store.MessageZh : zh.Trim(),
            string.IsNullOrWhiteSpace(en) ? _store.MessageEn : en.Trim());

        await _store.SaveAsync();
        return AdminActionResult.Ok("Kick message saved.");
    }
}

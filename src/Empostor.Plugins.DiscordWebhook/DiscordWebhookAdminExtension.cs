using Empostor.Api.Admin;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookAdminExtension : IAdminExtension
{
    private readonly DiscordWebhookStore _store;

    public DiscordWebhookAdminExtension(DiscordWebhookStore store)
    {
        _store = store;
    }

    public string Id => "discord-webhook";

    public string Title => "Discord Webhook";

    public string Icon => "webhook";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterBlock(block =>
        {
            block.Title = "Webhook URLs";
            block.Description = "Leave a URL empty to disable its notifications.";
            block.Children.Add(new AdminTextbox
            {
                Label = "Matchmaker URL",
                Value = _store.MatchmakerUrl,
                Placeholder = "https://discord.com/api/webhooks/...",
            }.OnSubmit(async ctx =>
            {
                _store.MatchmakerUrl = ctx.Value ?? string.Empty;
                await _store.SaveAsync();
                return AdminActionResult.Ok("Matchmaker webhook saved.");
            }));

            block.Children.Add(new AdminTextbox
            {
                Label = "Admin URL",
                Value = _store.AdminUrl,
                Placeholder = "https://discord.com/api/webhooks/...",
            }.OnSubmit(async ctx =>
            {
                _store.AdminUrl = ctx.Value ?? string.Empty;
                await _store.SaveAsync();
                return AdminActionResult.Ok("Admin webhook saved.");
            }));
        });
    }
}

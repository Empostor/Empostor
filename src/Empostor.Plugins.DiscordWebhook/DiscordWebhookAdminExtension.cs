using System;
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

        b.RegisterBlock(block =>
        {
            block.Title = "Live room tracking";
            block.Description = "With live room updates on, each lobby gets one Discord message that is edited as the "
                + "lobby changes and deleted when the lobby closes. Player Banned / Player Reported always post "
                + "one-shot messages to the Admin URL.";

            block.Children.Add(new AdminToggle
            {
                Label = "Live room updates",
                Value = _store.LiveRoomUpdates,
            }.OnChange(async ctx =>
            {
                _store.LiveRoomUpdates = string.Equals(ctx.Value, "true", StringComparison.OrdinalIgnoreCase);
                await _store.SaveAsync();
                return AdminActionResult.Ok($"Live room updates {(_store.LiveRoomUpdates ? "enabled" : "disabled")}.");
            }));

            block.Children.Add(new AdminToggle
            {
                Label = "Delete the message when the lobby closes",
                Value = _store.DeleteOnClose,
            }.OnChange(async ctx =>
            {
                _store.DeleteOnClose = string.Equals(ctx.Value, "true", StringComparison.OrdinalIgnoreCase);
                await _store.SaveAsync();
                return AdminActionResult.Ok($"Delete on close {(_store.DeleteOnClose ? "enabled" : "disabled")}.");
            }));

            block.Children.Add(new AdminNumber
            {
                Label = "Minimum seconds between two live updates",
                Value = _store.MinUpdateIntervalSeconds,
                Min = 1,
                Max = 60,
            }.OnChange(async ctx =>
            {
                _store.MinUpdateIntervalSeconds = int.TryParse(ctx.Value, out var seconds)
                    ? Math.Clamp(seconds, 1, 60)
                    : 3;
                await _store.SaveAsync();
                return AdminActionResult.Ok($"Minimum live update interval set to {_store.MinUpdateIntervalSeconds}s.");
            }));
        });
    }
}

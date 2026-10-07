using System;
using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookCommand : ICommand
{
    private readonly LiveRoomToggle _toggle;
    private readonly DiscordWebhookListener _listener;

    public DiscordWebhookCommand(LiveRoomToggle toggle, DiscordWebhookListener listener)
    {
        _toggle = toggle;
        _listener = listener;
    }

    public string Name => "dcwb";
    public string LocalizationOwner => DiscordWebhookPlugin.Owner;

    public string[] Aliases => new[] { "discordwebhook" };

    public string Description => "Turn this room's Discord posting on or off. Host only.";

    public string Usage => "dcwb <on|off>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.Sender.IsHost)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "discordwebhook.host_only", "Only the host can toggle Discord posting."),
                ctx.PlayerControl);
            return true;
        }

        if (ctx.Args.Length == 0)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "discordwebhook.usage", "Usage: #dcwb <on|off>"), ctx.PlayerControl);
            return true;
        }

        var arg = ctx.Args[0].Trim().ToLowerInvariant();
        bool enabled;
        if (arg == "on" || arg == "true" || arg == "1")
        {
            enabled = true;
        }
        else if (arg == "off" || arg == "false" || arg == "0")
        {
            enabled = false;
        }
        else
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "discordwebhook.usage", "Usage: #dcwb <on|off>"), ctx.PlayerControl);
            return true;
        }

        if (enabled == !_toggle.IsMuted(ctx.Game.Code))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "discordwebhook.already", "Discord posting is already {0} for this room.", State(ctx, enabled)),
                ctx.PlayerControl);
            return true;
        }

        _listener.SetRoomForwarding(ctx.Game, enabled);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            T(ctx, "discordwebhook.changed", "Discord posting {0} for this room.", State(ctx, enabled)),
            ctx.PlayerControl);

        return true;
    }

    private static string State(CommandContext ctx, bool enabled)
        => enabled
            ? T(ctx, "discordwebhook.state_on", "on")
            : T(ctx, "discordwebhook.state_off", "off");

    private static string T(CommandContext ctx, string key, string defaultText, params object?[] arguments)
    {
        return ctx.GetPluginString(DiscordWebhookPlugin.Owner, key, arguments);
    }
}

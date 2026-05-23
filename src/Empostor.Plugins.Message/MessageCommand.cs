using System;
using System.Text;
using System.Threading.Tasks;
using Empostor.Api.Commands;

namespace Empostor.Plugins.Message;

public sealed class MessageCommand : ICommand
{
    private readonly MessageStore _store;
    private readonly MessageConfig _config;

    public MessageCommand(MessageStore store, MessageConfig config)
    {
        _store = store;
        _config = config;
    }

    public string Name => "msg";
    public string[] Aliases => new[] { "message", "leave" };
    public string Description => "Leave a message for a player by friend code.";
    public string Usage => "msg <friendcode> <message>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (ctx.Args.Length < 2)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.usage", "Usage: #msg <friendcode> <message>"), ctx.PlayerControl);
            return true;
        }

        var targetFc = ctx.Args[0].Trim();
        var content = ctx.RawArgs!.Substring(ctx.Args[0].Length).Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.empty", "Message cannot be empty."), ctx.PlayerControl);
            return true;
        }

        // Validate friend code format
        if (targetFc.Length < 4)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.invalid_fc", "Invalid friend code format."), ctx.PlayerControl);
            return true;
        }

        var senderFc = ctx.Sender.Client.FriendCode ?? "";
        if (string.Equals(senderFc, targetFc, StringComparison.OrdinalIgnoreCase))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.self", "You cannot send a message to yourself."), ctx.PlayerControl);
            return true;
        }

        if (content.Length > _config.MessageMaxLength)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.too_long", "Message is too long.")
                    .Replace("{0}", _config.MessageMaxLength.ToString()),
                ctx.PlayerControl);
            return true;
        }

        if (_store.Count(targetFc) >= _config.MaxMessagesPerTarget)
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "message.full", "Already has too many pending messages.")
                    .Replace("{0}", targetFc),
                ctx.PlayerControl);
            return true;
        }

        var senderName = ctx.Sender.Character?.PlayerInfo?.PlayerName
                      ?? ctx.Sender.Client.Name
                      ?? "Unknown";

        var msg = new PendingMessage
        {
            SenderName = senderName,
            SenderFc = senderFc,
            TargetFc = targetFc,
            Content = content,
            Timestamp = DateTime.UtcNow,
        };

        _store.Add(msg);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            T(ctx, "message.sent", "Message sent to {0}.").Replace("{0}", targetFc),
            ctx.PlayerControl);

        return true;
    }

    private static string T(CommandContext ctx, string key, string defaultText)
    {
        string result = ctx.Lang.Get(key, ctx.SenderLanguage);
        return result == key ? defaultText : result;
    }
}

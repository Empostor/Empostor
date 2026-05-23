using System.Threading.Tasks;
using Empostor.Api.Commands;
using Empostor.Api.Service.Admin.Verify;

namespace Empostor.Plugins.QqVerify;

public sealed class QqVerifyCommand : ICommand
{
    private readonly IVerifyStore _store;

    public QqVerifyCommand(IVerifyStore store)
    {
        _store = store;
    }

    public string Name => "verify";

    public string[] Aliases => new[] { "ver" };

    public string Description => "Verify your QQ number for group access.";

    public string Usage => "verify <QQ号>";

    public async ValueTask<bool> ExecuteAsync(CommandContext ctx)
    {
        if (!ctx.IsSenderChinese())
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "qqverify.chinese_only",
                    "This command is only available for Chinese language players.\n此功能仅支持简体/繁体中文玩家使用。"),
                ctx.PlayerControl);
            return true;
        }

        var fc = ctx.Sender.Client.FriendCode;
        if (string.IsNullOrEmpty(fc))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "qqverify.no_friendcode",
                    "Unable to get your friend code. Please make sure you are logged in."),
                ctx.PlayerControl);
            return true;
        }

        var qqNumber = ctx.RawArgs?.Trim();
        if (string.IsNullOrEmpty(qqNumber) || !long.TryParse(qqNumber, out _))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                T(ctx, "qqverify.usage_message",
                    "Usage: #verify <your QQ number>\nExample: #verify 123456789"),
                ctx.PlayerControl);
            return true;
        }

        _store.AddPending(fc, qqNumber);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            T(ctx, "qqverify.recorded",
                    "Verification request recorded! Send /验证 {0} to the QQ bot.\nNote: The code is valid for 10 minutes.")
                .Replace("{0}", fc),
            ctx.PlayerControl);

        return true;
    }

    private static string T(CommandContext ctx, string key, string defaultText)
    {
        string result = ctx.Lang.Get(key, ctx.SenderLanguage);
        return result == key ? defaultText : result;
    }
}

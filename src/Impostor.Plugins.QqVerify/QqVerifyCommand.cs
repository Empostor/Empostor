using System.Threading.Tasks;
using Impostor.Api.Commands;
using Impostor.Api.Service.Admin.Verify;

namespace Impostor.Plugins.QqVerify;

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
                "此功能仅支持简体/繁体中文玩家使用。\nThis command is only available for Chinese language players.", ctx.PlayerControl);
            return true;
        }

        var fc = ctx.Sender.Client.FriendCode;
        if (string.IsNullOrEmpty(fc))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                "无法获取你的好友代码，请确保已登录账号。", ctx.PlayerControl);
            return true;
        }

        var qqNumber = ctx.RawArgs?.Trim();
        if (string.IsNullOrEmpty(qqNumber) || !long.TryParse(qqNumber, out _))
        {
            await ctx.PlayerControl.SendChatToPlayerAsync(
                "用法：/verify <你的QQ号>\n示例：/verify 123456789", ctx.PlayerControl);
            return true;
        }

        _store.AddPending(fc, qqNumber);

        await ctx.PlayerControl.SendChatToPlayerAsync(
            $"已记录验证请求！请私聊QQ机器人发送：/验证 {fc}\n注意：验证码10分钟内有效。", ctx.PlayerControl);

        return true;
    }
}

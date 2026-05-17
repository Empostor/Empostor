using System.Text.Json.Serialization;
using Impostor.Api.Service.Admin.Verify;
using Microsoft.AspNetCore.Mvc;

namespace Impostor.Server.Http;

[Route("/api/verify")]
[ApiController]
public sealed class VerifyController : ControllerBase
{
    private readonly IVerifyStore _store;

    public VerifyController(IVerifyStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Called by the QQ bot when a user sends /验证 &lt;好友代码&gt; in a group.
    /// Validates the FriendCode ↔ QQ pairing and removes the pending entry on success.
    /// </summary>
    [HttpPost("confirm")]
    public IActionResult Confirm([FromBody] VerifyConfirmRequest req)
    {
        if (!_store.ValidateSecret(req.Secret))
        {
            return Unauthorized(new { ok = false, error = "Invalid bot secret." });
        }

        if (string.IsNullOrWhiteSpace(req.FriendCode) || string.IsNullOrWhiteSpace(req.QqNumber))
        {
            return BadRequest(new { ok = false, error = "friendCode and qqNumber are required." });
        }

        var ok = _store.TryConfirm(req.FriendCode, req.QqNumber);
        if (ok)
        {
            return Ok(new { ok = true });
        }

        return Ok(new
        {
            ok = false,
            error = "no_pending_or_expired",
            message = "未找到待验证记录或已过期。请先在游戏内输入 /verify <你的QQ号>。",
        });
    }

    public sealed record VerifyConfirmRequest(
        [property: JsonPropertyName("friendCode")] string FriendCode,
        [property: JsonPropertyName("qqNumber")] string QqNumber,
        [property: JsonPropertyName("secret")] string Secret);
}

using Microsoft.AspNetCore.Http;

namespace Empostor.Server.Http.Admin;

/// <summary>
///     Shared admin-session check for the controllers that expose <c>/api/admin/*</c> endpoints,
///     so every one of them validates the same cookie against the same password hash.
/// </summary>
internal static class AdminSession
{
    public const string CookieName = "empostor_admin";

    public static bool IsAuthenticated(HttpContext context, string passwordHash)
        => !string.IsNullOrEmpty(passwordHash)
           && context.Request.Cookies.TryGetValue(CookieName, out var value)
           && AdminController.ConstantTimeEquals(value, passwordHash);
}

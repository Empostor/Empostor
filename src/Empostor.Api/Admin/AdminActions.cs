using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Empostor.Api.Admin;

public delegate ValueTask<AdminActionResult> AdminAction(AdminActionContext context);

/// <summary>
///     The result of an admin action. Tells the renderer what to do next.
/// </summary>
/// <param name="Success">Whether the action completed successfully.</param>
/// <param name="Message">An optional toast message shown to the operator.</param>
/// <param name="Refresh">When true, the client re-fetches the panel to reflect new state.</param>
public sealed record AdminActionResult(bool Success, string? Message = null, bool Refresh = true)
{
    public static AdminActionResult Ok(string? message = null, bool refresh = true) => new(true, message, refresh);

    public static AdminActionResult Fail(string message) => new(false, message, true);
}

public sealed class AdminActionContext
{
    public AdminActionContext(
        string extensionId,
        string actionId,
        string? value,
        JsonElement? payload,
        IServiceProvider services)
    {
        ExtensionId = extensionId;
        ActionId = actionId;
        Value = value;
        Payload = payload;
        Services = services;
    }

    /// <summary>The extension that owns the invoked action.</summary>
    public string ExtensionId { get; }

    /// <summary>The action id that was invoked.</summary>
    public string ActionId { get; }

    /// <summary>
    ///     The new value submitted by the widget (textbox text, toggle on/off, select value,
    ///     number value, chip value). <c>null</c> for plain button clicks.
    /// </summary>
    public string? Value { get; }

    /// <summary>The full JSON body of the request, if the widget sent structured data.</summary>
    public JsonElement? Payload { get; }

    /// <summary>The DI container, for resolving services the handler may need.</summary>
    public IServiceProvider Services { get; }
}

using System.Collections.Generic;

namespace Empostor.Api.Admin;

/// <summary>
///     A theme that reskins the admin panel, in the spirit of VuePress's "theme as plugin".
///     A theme can also be shipped as a folder under <c>Pages/themes/{Id}/theme.json</c>.
/// </summary>
public interface IAdminTheme
{
    /// <summary>Unique id. Also the folder name for disk-based themes.</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Id of a parent theme whose tokens/css/layout this theme inherits.</summary>
    string? Extends => null;

    /// <summary>Build the theme definition (tokens, palette, css, layout).</summary>
    AdminThemeDefinition Define();
}

/// <summary>
///     Declarative description of a theme's styling layers.
/// </summary>
public sealed class AdminThemeDefinition
{
    /// <summary>
    ///     Design tokens for the default (light) appearance, keyed by CSS custom-property name
    ///     (e.g. <c>--bg</c>, <c>--a</c>). Use the names in <see cref="AdminThemeTokens"/>.
    /// </summary>
    public Dictionary<string, string> Tokens { get; set; } = new();

    /// <summary>
    ///     Dark-appearance overrides applied under <c>[data-theme="dark"]</c>. When omitted,
    ///     the framework keeps the light tokens for both modes.
    /// </summary>
    public Dictionary<string, string>? DarkTokens { get; set; }

    /// <summary>Raw CSS appended after the tokens (palette/component styles, animations).</summary>
    public string? CustomCss { get; set; }

    /// <summary>
    ///     Optional layout template. Slots are <c>{{slot:header}}</c>, <c>{{slot:nav}}</c>,
    ///     <c>{{slot:content}}</c> and <c>{{slot:footer}}</c>. When omitted the built-in layout is used.
    /// </summary>
    public string? Layout { get; set; }

    /// <summary>Named component aliases (component name → HTML fragment) usable in the layout.</summary>
    public Dictionary<string, string> Components { get; set; } = new();
}

/// <summary>
///     The standard set of design-token names the admin panel consumes. Themes should fill
///     these keys so their palette applies to every built-in control.
/// </summary>
public static class AdminThemeTokens
{
    public const string Background = "--bg";
    public const string Surface = "--s";
    public const string Border = "--b";
    public const string Text = "--t";
    public const string Muted = "--m";
    public const string Accent = "--a";
    public const string Success = "--g";
    public const string Warning = "--y";
    public const string Danger = "--r";
    public const string Purple = "--p";
    public const string Orange = "--o";
    public const string Field = "--field";
    public const string Hover = "--hover";
    public const string OnAccent = "--on-a";
}

namespace Empostor.Api.Utils;

/// <summary>
///     Allows plugins to register a custom module tag for coloured
///     console output.  Inject via DI and call <see cref="Register" />
///     during plugin construction.
/// </summary>
public interface IModuleTagRegistry
{
    /// <summary>
    ///     Register a namespace → tag mapping.
    /// </summary>
    /// <param name="namespacePrefix">
    ///     Full or partial namespace prefix, e.g. "Empostor.Plugin.MyMod".
    /// </param>
    /// <param name="tag">
    ///     Short tag shown in console brackets, e.g. "MyMod".
    /// </param>
    /// <param name="ansiColor">
    ///     Optional ANSI SGR colour, e.g. "\x1b[92m" for bright green.
    ///     Omit for auto-assignment from the palette.
    /// </param>
    void Register(string namespacePrefix, string tag, string? ansiColor = null);
}

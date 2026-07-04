using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Empostor.Api.Utils;

namespace Empostor.Server.Utils;

/// <summary>
///     Thread-safe registry for plugin module tags.
///     Plugins inject <see cref="IModuleTagRegistry" /> and call <see cref="Register" /> to assign a short tag (and optional ANSI colour) to their namespace prefix.
///     The enricher and console formatter consult this registry at runtime.
/// </summary>
public sealed class ModuleTagRegistry : IModuleTagRegistry
{
    public static ModuleTagRegistry Instance { get; } = new();

    private readonly List<(string Prefix, string Tag, string? Color)> _entries = new();
    private readonly ConcurrentDictionary<string, string> _colorCache = new();

    private int _autoIdx = -1;

    private static readonly string[] AutoPalette =
    {
        "\x1b[96m", // Bright Cyan
        "\x1b[95m", // Bright Magenta
        "\x1b[94m", // Bright Blue
        "\x1b[92m", // Bright Green
        "\x1b[93m", // Bright Yellow
        "\x1b[91m", // Bright Red
    };

    private ModuleTagRegistry()
    {
    }

    /// <summary>
    ///     Register a namespace → tag mapping.
    /// </summary>
    /// <param name="namespacePrefix">Full or partial namespace prefix, e.g. "Empostor.Plugin.MyMod".</param>
    /// <param name="tag">Short tag shown in brackets, e.g. "MyMod" (keep it short).</param>
    /// <param name="ansiColor">
    ///     Optional ANSI SGR colour code (e.g. "\x1b[92m" for bright green).
    ///     When omitted a colour is auto-assigned from the palette.
    /// </param>
    public void Register(string namespacePrefix, string tag, string? ansiColor = null)
    {
        var color = ansiColor ?? NextAutoColor();
        _entries.Add((namespacePrefix, tag, color));
        _colorCache[tag] = color;
    }

    /// <summary>
    ///     Resolve a full <c>SourceContext</c> string to a (tag, colour) pair.
    ///     Returns ("Plugin", null) when no registered prefix matches.
    /// </summary>
    public (string Tag, string? Color) Resolve(string sourceContext)
    {
        foreach (var (prefix, tag, color) in _entries)
        {
            if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
                return (tag, color);
        }

        return ("Plugin", null);
    }

    public string? GetColor(string tag)
    {
        _colorCache.TryGetValue(tag, out var c);
        return c;
    }

    private string NextAutoColor()
    {
        var i = Interlocked.Increment(ref _autoIdx);
        return AutoPalette[Math.Abs(i) % AutoPalette.Length];
    }
}

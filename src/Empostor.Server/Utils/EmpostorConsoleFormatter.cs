using System;
using System.Collections.Generic;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace Empostor.Server.Utils;

public sealed class EmpostorConsoleFormatter : ITextFormatter
{
    private readonly bool _useColor;

    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim = "\x1b[2m";

    private const string FgRed = "\x1b[91m";
    private const string FgGreen = "\x1b[92m";
    private const string FgYellow = "\x1b[93m";
    private const string FgBlue = "\x1b[94m";
    private const string FgMagenta = "\x1b[95m";
    private const string FgCyan = "\x1b[96m";
    private const string FgWhite = "\x1b[97m";
    private const string FgGray = "\x1b[90m";
    private const string FgDark = "\x1b[37m"; 

    private const string BgRed = "\x1b[41m";

    private static readonly Dictionary<string, string> ModuleColors = new()
    {
        ["Auth"] = FgMagenta,
        ["Game"] = FgCyan,
        ["Chat"] = FgGreen,
        ["Reactor"] = "\x1b[35m",
        ["Admin"] = FgBlue,
        ["Cmd"] = FgYellow,
        ["Ban"] = FgRed,
        ["Plugin"] = "\x1b[36m",
        ["Discord"] = "\x1b[34m",
        ["Stat"] = FgCyan,
    };

    public EmpostorConsoleFormatter()
    {
        _useColor = !Console.IsOutputRedirected;
    }

    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (_useColor)
        {
            FormatColored(logEvent, output);
        }
        else
        {
            FormatPlain(logEvent, output);
        }
    }

    private void FormatColored(LogEvent logEvent, TextWriter output)
    {
        var ts = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        output.Write(Dim);
        output.Write(FgGray);
        output.Write(ts);
        output.Write(Reset);
        output.Write(' ');

        var (levelFg, levelBg, levelText) = LevelStyle(logEvent.Level);
        if (levelBg.Length > 0)
        {
            output.Write(levelBg);
        }

        output.Write(levelFg);
        output.Write(Bold);
        output.Write(levelText);
        output.Write(Reset);
        output.Write(' ');

        var module = GetModule(logEvent);
        var modColor = GetModuleColor(module);
        output.Write(modColor);
        output.Write('[');
        output.Write(module);
        output.Write(']');
        output.Write(Reset);
        output.Write(' ');

        output.Write(logEvent.RenderMessage());
        output.WriteLine();

        if (logEvent.Exception != null)
        {
            output.Write(FgRed);
            output.Write(logEvent.Exception);
            output.Write(Reset);
            output.WriteLine();
        }
    }

    private static void FormatPlain(LogEvent logEvent, TextWriter output)
    {
        var ts = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var level = FormatLevel(logEvent.Level);
        var module = GetModule(logEvent);

        output.Write($"{ts} [{level}] [{module}] ");
        output.Write(logEvent.RenderMessage());
        output.WriteLine();

        if (logEvent.Exception != null)
        {
            output.Write(logEvent.Exception.ToString());
            output.WriteLine();
        }
    }

    private static string GetModule(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("Module", out var v) &&
            v is ScalarValue sv && sv.Value is string module)
        {
            return module;
        }

        return "Empostor";
    }

    private static string GetModuleColor(string module)
    {
        if (ModuleColors.TryGetValue(module, out var c))
            return c;

        // Check plugin-registered tags
        var regColor = ModuleTagRegistry.Instance.GetColor(module);
        return regColor ?? FgWhite;
    }

    private static string FormatLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "INF",
    };

    private static (string fg, string bg, string text) LevelStyle(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal => (FgWhite, BgRed, "FTL"),
        LogEventLevel.Error => (FgRed, string.Empty, "ERR"),
        LogEventLevel.Warning => (FgYellow, string.Empty, "WRN"),
        LogEventLevel.Information => (FgGray, string.Empty, "INF"),
        LogEventLevel.Debug => (FgDark, string.Empty, "DBG"),
        LogEventLevel.Verbose => (Dim + FgDark, string.Empty, "VRB"),
        _ => (FgGray, string.Empty, "INF"),
    };
}

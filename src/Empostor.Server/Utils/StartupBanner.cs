using System;
using System.IO;
using System.Linq;
using Empostor.Api.Config;
using Empostor.Api.Utils;
using Microsoft.Extensions.Configuration;

namespace Empostor.Server.Utils;

internal static class StartupBanner
{
    private const string Bold = "\x1b[1m";
    private const string Dim = "\x1b[2m";
    private const string Reset = "\x1b[0m";

    private const string Cyan = "\x1b[96m";
    private const string Green = "\x1b[92m";
    private const string Yellow = "\x1b[93m";
    private const string Red = "\x1b[91m";
    private const string White = "\x1b[97m";
    private const string Gray = "\x1b[90m";

    private const string BoxH = "─";
    private const string BoxV = "│";
    private const string BoxTL = "┌";
    private const string BoxTR = "┐";
    private const string BoxBL = "└";
    private const string BoxBR = "┘";
    private const string BoxML = "├";
    private const string BoxMR = "┤";

    private static readonly bool UseColor = !Console.IsOutputRedirected;

    public static void Print(IConfiguration configuration)
    {
        var prevEncoding = Console.OutputEncoding;
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch { /* best-effort */ }

        try
        {
            PrintInternal(configuration);
        }
        finally
        {
            try { Console.OutputEncoding = prevEncoding; }
            catch { /* ignore */ }
        }
    }

    private static void PrintInternal(IConfiguration configuration)
    {
        var server = configuration.GetSection(ServerConfig.Section).Get<ServerConfig>() ?? new ServerConfig();
        var http = configuration.GetSection(HttpServerConfig.Section).Get<HttpServerConfig>() ?? new HttpServerConfig();
        var anticheat = configuration.GetSection(AntiCheatConfig.Section).Get<AntiCheatConfig>() ?? new AntiCheatConfig();
        var logLevel = configuration.GetValue<string>("Serilog:MinimumLevel") ?? "Information";
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "Log", $"empostor-{DateTime.Now:yyyy-MM-dd}.log");

        var version = DotnetUtils.Version;

        string[] labelTexts = { "Public IP", "Listen Port", "HTTP API", "Admin Panel", "Port Pool", "AntiCheat", "Log Level", "Log File" };
        var maxLabel = labelTexts.Max(t => t.Length) + 2;

        var width = 62;

        using var sw = new StringWriter();

        sw.WriteLine(C(BoxTL + new string(BoxH[0], width - 2) + BoxTR, Cyan));

        Center(sw, width, Bold + White + "Empostor" + Reset + C(" v" + version, Cyan));
        Center(sw, width, C("Among Us Private Server", Gray));
        sw.WriteLine(C(BoxML + new string(BoxH[0], width - 2) + BoxMR, Cyan));

        Row(sw, width, maxLabel, "Public IP", server.PublicIp);
        var deltaEnabled = server.DeltaPortStart > 0 && server.DeltaPortEnd >= server.DeltaPortStart;
        var actualListenPort = deltaEnabled && server.ReserveLastDeltaPortAsDefault
            ? server.DeltaPortEnd
            : server.ListenPort;
        Row(sw, width, maxLabel, "Listen Port", $"{actualListenPort} (UDP)");
        Row(sw, width, maxLabel, "HTTP API", $"http://{http.ListenIp}:{http.ListenPort}");
        Row(sw, width, maxLabel, "Admin Panel", $"http://{server.PublicIp}:{http.ListenPort}/admin");
        var poolInfo = deltaEnabled
            ? $"{server.DeltaPortStart}-{server.DeltaPortEnd} ({server.DeltaPortEnd - server.DeltaPortStart + 1} ports)"
            : C("disabled", Yellow);
        Row(sw, width, maxLabel, "Port Pool", poolInfo);
        if (deltaEnabled && server.ReserveLastDeltaPortAsDefault)
        {
            Row(sw, width, maxLabel, "Reserved", $"{server.DeltaPortEnd} (main listener)");
        }

        Row(sw, width, maxLabel, "Packet Filter", C("✔ Enabled", Green));
        Row(sw, width, maxLabel, "AntiCheat", anticheat.Enabled ? C("✔ Enabled", Green) : C("✗ Disabled", Red));
        Row(sw, width, maxLabel, "Log Level", logLevel);
        Row(sw, width, maxLabel, "Log File", TrimPath(logPath, width - maxLabel - 5));

        sw.WriteLine(C(BoxBL + new string(BoxH[0], width - 2) + BoxBR, Cyan));

        Console.WriteLine(sw.ToString());
    }

    private static void Row(StringWriter sw, int width, int labelW, string label, string value)
    {
        sw.Write(C(BoxV, Cyan));
        sw.Write(' ');
        sw.Write(C(PadRight(label, labelW), White));
        sw.Write(C(BoxV, Cyan));
        sw.Write(' ');
        sw.Write(C(value, Gray));

        var used = 4 + labelW + VisibleLength(value);
        var remain = width - 2 - used;
        if (remain > 0)
        {
            sw.Write(new string(' ', remain));
        }

        sw.WriteLine(C(BoxV, Cyan));
    }

    private static void Center(StringWriter sw, int width, string text)
    {
        var vis = VisibleLength(text);
        var pad = (width - vis - 2) / 2;
        sw.Write(C(BoxV, Cyan));
        if (pad > 0)
        {
            sw.Write(new string(' ', pad));
        }

        sw.Write(text);
        var right = width - vis - pad - 2;
        if (right > 0)
        {
            sw.Write(new string(' ', right));
        }

        sw.WriteLine(C(BoxV, Cyan));
    }

    private static string C(string text, string ansi) =>
        UseColor ? $"{ansi}{text}{Reset}" : text;

    private static int VisibleLength(string text)
    {
        if (!UseColor)
        {
            return text.Length;
        }

        var len = 0;
        var inEscape = false;
        foreach (var ch in text)
        {
            if (ch == '\x1b') { inEscape = true; continue; }
            if (inEscape) { if (ch == 'm') { inEscape = false; } continue; }
            len++;
        }

        return len;
    }

    private static string PadRight(string text, int width)
    {
        var vis = VisibleLength(text);
        return text + new string(' ', Math.Max(0, width - vis));
    }

    private static string TrimPath(string path, int maxLen)
    {
        if (maxLen < 5)
        {
            return path;
        }

        if (path.Length <= maxLen)
        {
            return path;
        }

        return "…" + path[^(maxLen - 1)..];
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Welcome.Service;

public sealed class WelcomeMessageService
{
    private static readonly string MessagesDir =
        Path.Combine(AppContext.BaseDirectory, "Messages");

    private static readonly string HelloFile =
        Path.Combine(MessagesDir, "HelloWorld.txt");

    private readonly ILogger<WelcomeMessageService> _logger;

    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);

    public WelcomeMessageService(ILogger<WelcomeMessageService> logger)
    {
        _logger = logger;
    }

    public void EnsureDefaults()
    {
        if (!Directory.Exists(MessagesDir))
            Directory.CreateDirectory(MessagesDir);

        if (!File.Exists(HelloFile))
        {
            File.WriteAllText(HelloFile, DefaultContent);
            _logger.LogInformation("[Welcome] Created Messages/HelloWorld.txt");
        }

        Reload();
    }

    public void Reload()
    {
        _templates.Clear();
        if (!File.Exists(HelloFile)) return;

        foreach (var raw in File.ReadAllLines(HelloFile))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var colon = line.IndexOf(':');
            if (colon < 1) continue;

            var lang = line[..colon].Trim();
            var msg  = line[(colon + 1)..].Trim();
            if (!string.IsNullOrEmpty(msg))
                _templates[lang] = msg;
        }

        _logger.LogInformation("[Welcome] Loaded {Count} language template(s) from HelloWorld.txt",
            _templates.Count);
    }

    public string GetMessage(string name, string? friendCode, string gameCode, string languageCode = "default")
    {
        var template = _templates.TryGetValue(languageCode, out var t)
            ? t
            : _templates.TryGetValue("default", out var d)
                ? d
                : "Welcome, {Name}!";

        return template
            .Replace("{Name}", name, StringComparison.OrdinalIgnoreCase)
            .Replace("{FriendCode}", friendCode ?? "None", StringComparison.OrdinalIgnoreCase)
            .Replace("{GameCode}",   gameCode, StringComparison.OrdinalIgnoreCase);
    }

    private const string DefaultContent = """
# Empostor Welcome Messages — Messages/HelloWorld.txt
# Placeholders:
#   {Name}       — player display name
#   {FriendCode} — player friend code (e.g. Alice#1234)
#   {GameCode}   — room code (e.g. ABCDEF)
#
# Add a line per language using the matching lang code.
# Supported lang codes: en, zh, ko, ru, pt, de, fr, es, ja, id, and more.

default:  Welcome, {Name}! Friend code: {FriendCode} | Room: {GameCode}
en: Welcome, {Name}! Friend code: {FriendCode} | Room: {GameCode}
zh: 欢迎，{Name}！好友代码：{FriendCode} | 房号：{GameCode}
ko: 환영합니다, {Name}! 친구 코드: {FriendCode} | 방: {GameCode}
ru: Добро пожаловать, {Name}! Код друга: {FriendCode} | Комната: {GameCode}
pt: Bem-vindo, {Name}! Código de amigo: {FriendCode} | Sala: {GameCode}
de: Willkommen, {Name}! Freundescode: {FriendCode} | Raum: {GameCode}
fr: Bienvenue, {Name}! Code ami : {FriendCode} | Salle : {GameCode}
es: ¡Bienvenido, {Name}! Código amigo: {FriendCode} | Sala: {GameCode}
ja: ようこそ、{Name}！フレンドコード：{FriendCode} | ルーム：{GameCode}
""";
}

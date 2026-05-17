using System;
using System.IO;
using System.Threading.Tasks;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Welcome;

[ImpostorPlugin("cn.hayashiume.welcome", "Welcome Messages", "HayashiUme", "1.0.0")]
public sealed class WelcomePlugin : PluginBase
{
    private const string TextDir = "Message";

    private readonly ILogger<WelcomePlugin> _logger;

    public WelcomePlugin(ILogger<WelcomePlugin> logger)
    {
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        EnsureTextDirectory();
        EnsureDefaultFiles();
        _logger.LogInformation("[Welcome] Enabled. Edit Message/{{Language}}HelloWord.txt files to customise.");
        return default;
    }

    public override ValueTask DisableAsync() => default;

    private static void EnsureTextDirectory()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), TextDir);
        Directory.CreateDirectory(dir);
    }

    private void EnsureDefaultFiles()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), TextDir);

        WriteIfMissing(dir, "EnglishHelloWord.txt", "Welcome, {0}! Room: {1}");
        WriteIfMissing(dir, "SChineseHelloWord.txt", "欢迎，{0}！房间：{1}");
        WriteIfMissing(dir, "TChineseHelloWord.txt", "歡迎，{0}！房間：{1}");
        WriteIfMissing(dir, "KoreanHelloWord.txt", "환영합니다, {0}! 방: {1}");
        WriteIfMissing(dir, "RussianHelloWord.txt", "Добро пожаловать, {0}! Комната: {1}");
        WriteIfMissing(dir, "PortugueseHelloWord.txt", "Bem-vindo, {0}! Sala: {1}");
        WriteIfMissing(dir, "BrazilianHelloWord.txt", "Bem-vindo, {0}! Sala: {1}");
        WriteIfMissing(dir, "FrenchHelloWord.txt", "Bienvenue, {0}! Salle : {1}");
        WriteIfMissing(dir, "GermanHelloWord.txt", "Willkommen, {0}! Raum: {1}");
        WriteIfMissing(dir, "SpanishHelloWord.txt", "¡Bienvenido, {0}! Sala: {1}");
        WriteIfMissing(dir, "LatamHelloWord.txt", "¡Bienvenido, {0}! Sala: {1}");
        WriteIfMissing(dir, "JapaneseHelloWord.txt", "ようこそ、{0}！ルーム：{1}");
        WriteIfMissing(dir, "ItalianHelloWord.txt", "Benvenuto, {0}! Stanza: {1}");
        WriteIfMissing(dir, "DutchHelloWord.txt", "Welkom, {0}! Kamer: {1}");
        WriteIfMissing(dir, "FilipinoHelloWord.txt", "Maligayang pagdating, {0}! Silid: {1}");
        WriteIfMissing(dir, "IrishHelloWord.txt", "Fáilte, {0}! Seomra: {1}");
    }

    private void WriteIfMissing(string dir, string fileName, string content)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
            _logger.LogInformation("[Welcome] Created Message/{FileName}", fileName);
        }
    }
}

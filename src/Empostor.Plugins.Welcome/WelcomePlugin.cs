using System;
using System.IO;
using System.Threading.Tasks;
using Empostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Welcome;

[EmpostorPlugin("cn.hayashiume.welcome", "Welcome Messages", "HayashiUme", "1.1.0")]
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

        WriteIfMissing(dir, "EnglishHelloWord.txt", "Welcome, {Name}! Room: {Room}");
        WriteIfMissing(dir, "SChineseHelloWord.txt", "欢迎，{Name}！房间：{Room}");
        WriteIfMissing(dir, "TChineseHelloWord.txt", "歡迎，{Name}！房間：{Room}");
        WriteIfMissing(dir, "KoreanHelloWord.txt", "환영합니다, {Name}! 방: {Room}");
        WriteIfMissing(dir, "RussianHelloWord.txt", "Добро пожаловать, {Name}! Комната: {Room}");
        WriteIfMissing(dir, "PortugueseHelloWord.txt", "Bem-vindo, {Name}! Sala: {Room}");
        WriteIfMissing(dir, "BrazilianHelloWord.txt", "Bem-vindo, {Name}! Sala: {Room}");
        WriteIfMissing(dir, "FrenchHelloWord.txt", "Bienvenue, {Name}! Salle : {Room}");
        WriteIfMissing(dir, "GermanHelloWord.txt", "Willkommen, {Name}! Raum: {Room}");
        WriteIfMissing(dir, "SpanishHelloWord.txt", "¡Bienvenido, {Name}! Sala: {Room}");
        WriteIfMissing(dir, "LatamHelloWord.txt", "¡Bienvenido, {Name}! Sala: {Room}");
        WriteIfMissing(dir, "JapaneseHelloWord.txt", "ようこそ、{Name}！ルーム：{Room}");
        WriteIfMissing(dir, "ItalianHelloWord.txt", "Benvenuto, {Name}! Stanza: {Room}");
        WriteIfMissing(dir, "DutchHelloWord.txt", "Welkom, {Name}! Kamer: {Room}");
        WriteIfMissing(dir, "FilipinoHelloWord.txt", "Maligayang pagdating, {Name}! Silid: {Room}");
        WriteIfMissing(dir, "IrishHelloWord.txt", "Fáilte, {Name}! Seomra: {Room}");
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

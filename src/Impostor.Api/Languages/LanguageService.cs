using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Impostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Impostor.Api.Languages;

public sealed class LanguageService
{
    private static readonly string LangDir = Path.Combine(Directory.GetCurrentDirectory(), "Languages");
    public readonly ILogger<LanguageService> _logger;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _tables = new();
    private static readonly string FallbackLang = "en";

    private static readonly Dictionary<Language, string> LangCodeMap = new()
    {
        [Language.English] = "en",
        [Language.Latam] = "es",
        [Language.Brazilian] = "pt_BR",
        [Language.Portuguese] = "pt",
        [Language.Korean] = "ko",
        [Language.Russian] = "ru",
        [Language.Dutch] = "nl",
        [Language.Filipino] = "fil",
        [Language.French] = "fr",
        [Language.German] = "de",
        [Language.Italian] = "it",
        [Language.Japanese] = "ja",
        [Language.Spanish] = "es",
        [Language.SChinese] = "zh_CN",
        [Language.TChinese] = "zh_TW",
        [Language.Irish] = "ga",
    };

    public LanguageService(ILogger<LanguageService> logger)
    {
        _logger = logger;
        EnsureDefaults();
        LoadAll();
    }

    public LanguageString Get(string key, Language language = Language.English)
    {
        var code = LangCodeMap.TryGetValue(language, out var c) ? c : FallbackLang;
        var text = Lookup(key, code) ?? Lookup(key, FallbackLang) ?? key;
        return new LanguageString(text);
    }

    public LanguageString Get(string key, string langCode)
    {
        var text = Lookup(key, langCode) ?? Lookup(key, FallbackLang) ?? key;
        return new LanguageString(text);
    }

    public void Reload()
    {
        _tables.Clear();
        LoadAll();
        _logger.LogInformation("[Language] Reloaded all language files.");
    }

    private string? Lookup(string key, string langCode)
    {
        return _tables.TryGetValue(langCode, out var table) && table.TryGetValue(key, out var val)
            ? val
            : null;
    }

    private void LoadAll()
    {
        if (!Directory.Exists(LangDir))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(LangDir, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                var table = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
                _tables[code] = table;
                _logger.LogDebug("[Language] Loaded {Code} ({Count} keys)", code, table.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Language] Failed to load {File}", file);
            }
        }

        _logger.LogInformation("[Language] Loaded {Count} language file(s).", _tables.Count);
    }

    private void EnsureDefaults()
    {
        Directory.CreateDirectory(LangDir);
        WriteIfMissing("en.json", DefaultEn);
        WriteIfMissing("zh_CN.json", DefaultZhCn);
        WriteIfMissing("zh_TW.json", DefaultZhTw);
        WriteIfMissing("ko.json", DefaultKo);
        WriteIfMissing("ru.json", DefaultRu);
        WriteIfMissing("de.json", DefaultDe);
        WriteIfMissing("fr.json", DefaultFr);
        WriteIfMissing("ja.json", DefaultJa);
        WriteIfMissing("pt.json", DefaultPt);
        WriteIfMissing("pt_BR.json", DefaultPtBr);
        WriteIfMissing("es.json", DefaultEs);
        WriteIfMissing("it.json", DefaultIt);
        WriteIfMissing("nl.json", DefaultNl);
        WriteIfMissing("fil.json", DefaultFil);
        WriteIfMissing("ga.json", DefaultGa);
    }

    private void WriteIfMissing(string fileName, string content)
    {
        var path = Path.Combine(LangDir, fileName);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
        }
    }

    // Language Tranlations used AI.
    // Any Pr fix grammer wrongs is OK!
    private const string DefaultEn = """
{
  "command.unknown": "Unknown command /{0}. Type /help for a list.",
  "command.error": "An error occurred while executing /{0}.",
  "command.usage": "Usage: /{0}",
  "command.help.list": "=== Commands ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Unknown command: /{0}",
  "command.help.aliases": "Aliases: {0}",
  "command.note.host_only": "Only the host can use /note.",
  "command.note.cleared": "Note cleared.",
  "command.note.set": "Note set: {0}",
  "command.color.invalid": "Invalid color ID. Valid range: 0–17.",
  "command.color.set": "Color changed to {0} ({1}).",
  "command.title.too_long": "Title too long. Max 12 characters.",
  "command.title.cleared": "Title removed.",
  "command.title.set": "Title set: {0}",
  "command.stat.disabled": "Player statistics are not enabled on this server.",
  "command.stat.no_stats": "No statistics recorded yet. Play a game first!",
  "command.stat.header": "=== Your Statistics ===",
  "command.stat.games": "Games Played: {0}",
  "command.stat.wins": "Wins: {0}",
  "command.stat.losses": "Losses: {0}",
  "command.stat.impostor": "Impostor Wins: {0}",
  "command.stat.kills": "Kills: {0}",
  "command.stat.deaths": "Deaths: {0}",
  "command.stat.tasks": "Tasks Completed: {0}",
  "command.stat.exiled": "Times Exiled: {0}",
  "welcome.join": "Welcome, {0}! Friend code: {1} | Room: {2}"
}
""";

    private const string DefaultZhCn = """
{
  "command.unknown": "未知指令 /{0}。输入 /help 查看列表。",
  "command.error": "执行 /{0} 时发生错误。",
  "command.usage": "用法：/{0}",
  "command.help.list": "=== 指令列表 ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "未知指令：/{0}",
  "command.help.aliases": "别名：{0}",
  "command.note.host_only": "只有房主可以使用 /note。",
  "command.note.cleared": "备注已清除。",
  "command.note.set": "备注已设置：{0}",
  "command.color.invalid": "无效颜色 ID，范围：0–17。",
  "command.color.set": "颜色已更改为 {0}（{1}）。",
  "command.title.too_long": "头衔过长，最多 12 个字符。",
  "command.title.cleared": "头衔已移除。",
  "command.title.set": "头衔已设置：{0}",
  "command.stat.disabled": "此服务器未启用玩家统计。",
  "command.stat.no_stats": "暂无统计数据，先玩一局游戏吧！",
  "command.stat.header": "=== 你的统计 ===",
  "command.stat.games": "游戏场数：{0}",
  "command.stat.wins": "胜利：{0}",
  "command.stat.losses": "失败：{0}",
  "command.stat.impostor": "内鬼胜利：{0}",
  "command.stat.kills": "击杀：{0}",
  "command.stat.deaths": "死亡：{0}",
  "command.stat.tasks": "完成任务：{0}",
  "command.stat.exiled": "被票出：{0}",
  "welcome.join": "欢迎，{0}！好友代码：{1} | 房间：{2}"
}
""";

    private const string DefaultZhTw = """
{
  "command.unknown": "未知指令 /{0}。輸入 /help 查看列表。",
  "command.error": "執行 /{0} 時發生錯誤。",
  "command.usage": "用法：/{0}",
  "command.help.list": "=== 指令列表 ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "未知指令：/{0}",
  "command.help.aliases": "別名：{0}",
  "command.note.host_only": "只有房主可以使用 /note。",
  "command.note.cleared": "備註已清除。",
  "command.note.set": "備註已設置：{0}",
  "command.color.invalid": "無效顏色 ID，範圍：0–17。",
  "command.color.set": "顏色已更改為 {0}（{1}）。",
  "command.title.too_long": "頭銜過長，最多 12 個字符。",
  "command.title.cleared": "頭銜已移除。",
  "command.title.set": "頭銜已設置：{0}",
  "command.stat.disabled": "此伺服器未啟用玩家統計。",
  "command.stat.no_stats": "暫無統計數據，先玩一局遊戲吧！",
  "command.stat.header": "=== 你的統計 ===",
  "command.stat.games": "遊戲場數：{0}",
  "command.stat.wins": "勝利：{0}",
  "command.stat.losses": "失敗：{0}",
  "command.stat.impostor": "內鬼勝利：{0}",
  "command.stat.kills": "擊殺：{0}",
  "command.stat.deaths": "死亡：{0}",
  "command.stat.tasks": "完成任務：{0}",
  "command.stat.exiled": "被票出：{0}",
  "welcome.join": "歡迎，{0}！好代碼：{1} | 房間：{2}"
}
""";

    private const string DefaultKo = """
{
  "command.unknown": "알 수 없는 명령어 /{0}. /help를 입력하여 목록을 확인하세요.",
  "command.error": "/{0} 실행 중 오류가 발생했습니다.",
  "command.usage": "사용법: /{0}",
  "command.help.list": "=== 명령어 목록 ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "알 수 없는 명령어: /{0}",
  "command.help.aliases": "별칭: {0}",
  "command.note.host_only": "/note는 방장만 사용할 수 있습니다.",
  "command.note.cleared": "메모가 삭제되었습니다.",
  "command.note.set": "메모 설정됨: {0}",
  "command.color.invalid": "잘못된 색상 ID. 유효 범위: 0–17.",
  "command.color.set": "색상이 {0}({1})으로 변경되었습니다.",
  "command.title.too_long": "칭호가 너무 깁니다. 최대 12자.",
  "command.title.cleared": "칭호가 제거되었습니다.",
  "command.title.set": "칭호 설정됨: {0}",
  "command.stat.disabled": "이 서버에서는 플레이어 통계가 활성화되지 않았습니다.",
  "command.stat.no_stats": "아직 통계가 기록되지 않았습니다. 먼저 게임을 플레이하세요!",
  "command.stat.header": "=== 나의 통계 ===",
  "command.stat.games": "플레이한 게임: {0}",
  "command.stat.wins": "승리: {0}",
  "command.stat.losses": "패배: {0}",
  "command.stat.impostor": "임포스터 승리: {0}",
  "command.stat.kills": "킬: {0}",
  "command.stat.deaths": "사망: {0}",
  "command.stat.tasks": "완료한 작업: {0}",
  "command.stat.exiled": "추방됨: {0}",
  "welcome.join": "환영합니다, {0}! 친구 코드: {1} | 방: {2}"
}
""";

    private const string DefaultRu = """
{
  "command.unknown": "Неизвестная команда /{0}. Введите /help для списка.",
  "command.error": "Ошибка при выполнении /{0}.",
  "command.usage": "Использование: /{0}",
  "command.help.list": "=== Команды ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Неизвестная команда: /{0}",
  "command.help.aliases": "Псевдонимы: {0}",
  "command.note.host_only": "Только хост может использовать /note.",
  "command.note.cleared": "Заметка удалена.",
  "command.note.set": "Заметка установлена: {0}",
  "command.color.invalid": "Неверный ID цвета. Допустимый диапазон: 0–17.",
  "command.color.set": "Цвет изменён на {0} ({1}).",
  "command.title.too_long": "Титул слишком длинный. Максимум 12 символов.",
  "command.title.cleared": "Титул удалён.",
  "command.title.set": "Титул установлен: {0}",
  "command.stat.disabled": "Статистика игроков не включена на этом сервере.",
  "command.stat.no_stats": "Статистика пока не записана. Сыграйте сначала!",
  "command.stat.header": "=== Ваша статистика ===",
  "command.stat.games": "Игр сыграно: {0}",
  "command.stat.wins": "Побед: {0}",
  "command.stat.losses": "Поражений: {0}",
  "command.stat.impostor": "Побед за предателя: {0}",
  "command.stat.kills": "Убийств: {0}",
  "command.stat.deaths": "Смертей: {0}",
  "command.stat.tasks": "Заданий выполнено: {0}",
  "command.stat.exiled": "Изгнаний: {0}",
  "welcome.join": "Добро пожаловать, {0}! Код друга: {1} | Комната: {2}"
}
""";

    private const string DefaultDe = """
{
  "command.unknown": "Unbekannter Befehl /{0}. Tippe /help für eine Liste.",
  "command.error": "Fehler beim Ausführen von /{0}.",
  "command.usage": "Verwendung: /{0}",
  "command.help.list": "=== Befehle ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Unbekannter Befehl: /{0}",
  "command.help.aliases": "Aliase: {0}",
  "command.note.host_only": "Nur der Host kann /note verwenden.",
  "command.note.cleared": "Notiz gelöscht.",
  "command.note.set": "Notiz gesetzt: {0}",
  "command.color.invalid": "Ungültige Farb-ID. Gültiger Bereich: 0–17.",
  "command.color.set": "Farbe geändert zu {0} ({1}).",
  "command.title.too_long": "Titel zu lang. Maximal 12 Zeichen.",
  "command.title.cleared": "Titel entfernt.",
  "command.title.set": "Titel gesetzt: {0}",
  "command.stat.disabled": "Spielerstatistiken sind auf diesem Server nicht aktiviert.",
  "command.stat.no_stats": "Noch keine Statistiken. Spielen Sie zuerst ein Spiel!",
  "command.stat.header": "=== Deine Statistiken ===",
  "command.stat.games": "Gespielte Spiele: {0}",
  "command.stat.wins": "Siege: {0}",
  "command.stat.losses": "Niederlagen: {0}",
  "command.stat.impostor": "Impostor-Siege: {0}",
  "command.stat.kills": "Kills: {0}",
  "command.stat.deaths": "Tode: {0}",
  "command.stat.tasks": "Aufgaben erledigt: {0}",
  "command.stat.exiled": "Verbannt: {0}",
  "welcome.join": "Willkommen, {0}! Freundescode: {1} | Raum: {2}"
}
""";

    private const string DefaultFr = """
{
  "command.unknown": "Commande inconnue /{0}. Tapez /help pour la liste.",
  "command.error": "Erreur lors de l'exécution de /{0}.",
  "command.usage": "Utilisation : /{0}",
  "command.help.list": "=== Commandes ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Commande inconnue : /{0}",
  "command.help.aliases": "Alias : {0}",
  "command.note.host_only": "Seul l'hôte peut utiliser /note.",
  "command.note.cleared": "Note effacée.",
  "command.note.set": "Note définie : {0}",
  "command.color.invalid": "ID de couleur invalide. Plage valide : 0–17.",
  "command.color.set": "Couleur changée en {0} ({1}).",
  "command.title.too_long": "Titre trop long. Maximum 12 caractères.",
  "command.title.cleared": "Titre supprimé.",
  "command.title.set": "Titre défini : {0}",
  "command.stat.disabled": "Les statistiques des joueurs ne sont pas activées sur ce serveur.",
  "command.stat.no_stats": "Aucune statistique enregistrée. Jouez d'abord une partie !",
  "command.stat.header": "=== Vos statistiques ===",
  "command.stat.games": "Parties jouées : {0}",
  "command.stat.wins": "Victoires : {0}",
  "command.stat.losses": "Défaites : {0}",
  "command.stat.impostor": "Victoires Imposteur : {0}",
  "command.stat.kills": "Meurtres : {0}",
  "command.stat.deaths": "Morts : {0}",
  "command.stat.tasks": "Tâches accomplies : {0}",
  "command.stat.exiled": "Exilés : {0}",
  "welcome.join": "Bienvenue, {0} ! Code ami : {1} | Salle : {2}"
}
""";

    private const string DefaultJa = """
{
  "command.unknown": "不明なコマンド /{0}。/help でリストを確認してください。",
  "command.error": "/{0} の実行中にエラーが発生しました。",
  "command.usage": "使い方：/{0}",
  "command.help.list": "=== コマンド一覧 ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "不明なコマンド：/{0}",
  "command.help.aliases": "エイリアス：{0}",
  "command.note.host_only": "/note はホストのみ使用できます。",
  "command.note.cleared": "ノートを削除しました。",
  "command.note.set": "ノートを設定しました：{0}",
  "command.color.invalid": "無効なカラーID。有効範囲：0–17。",
  "command.color.set": "カラーを {0}（{1}）に変更しました。",
  "command.title.too_long": "称号が長すぎます。最大12文字。",
  "command.title.cleared": "称号を削除しました。",
  "command.title.set": "称号を設定しました：{0}",
  "command.stat.disabled": "このサーバーではプレイヤー統計が有効になっていません。",
  "command.stat.no_stats": "まだ統計が記録されていません。まずゲームをプレイしてください！",
  "command.stat.header": "=== あなたの統計 ===",
  "command.stat.games": "プレイしたゲーム：{0}",
  "command.stat.wins": "勝利：{0}",
  "command.stat.losses": "敗北：{0}",
  "command.stat.impostor": "インポスター勝利：{0}",
  "command.stat.kills": "キル：{0}",
  "command.stat.deaths": "デス：{0}",
  "command.stat.tasks": "タスク完了：{0}",
  "command.stat.exiled": "追放された回数：{0}",
  "welcome.join": "ようこそ、{0}！フレンドコード：{1} | ルーム：{2}"
}
""";

    private const string DefaultPt = """
{
  "command.unknown": "Comando desconhecido /{0}. Digite /help para ver a lista.",
  "command.error": "Ocorreu um erro ao executar /{0}.",
  "command.usage": "Uso: /{0}",
  "command.help.list": "=== Comandos ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Comando desconhecido: /{0}",
  "command.help.aliases": "Aliases: {0}",
  "command.note.host_only": "Apenas o anfitrião pode usar /note.",
  "command.note.cleared": "Nota removida.",
  "command.note.set": "Nota definida: {0}",
  "command.color.invalid": "ID de cor inválido. Intervalo válido: 0–17.",
  "command.color.set": "Cor alterada para {0} ({1}).",
  "command.title.too_long": "Título muito longo. Máximo 12 caracteres.",
  "command.title.cleared": "Título removido.",
  "command.title.set": "Título definido: {0}",
  "command.stat.disabled": "As estatísticas de jogadores não estão ativadas neste servidor.",
  "command.stat.no_stats": "Nenhuma estatística registada. Jogue primeiro!",
  "command.stat.header": "=== As suas estatísticas ===",
  "command.stat.games": "Jogos: {0}",
  "command.stat.wins": "Vitórias: {0}",
  "command.stat.losses": "Derrotas: {0}",
  "command.stat.impostor": "Vitórias Impostor: {0}",
  "command.stat.kills": "Assassínios: {0}",
  "command.stat.deaths": "Mortes: {0}",
  "command.stat.tasks": "Tarefas: {0}",
  "command.stat.exiled": "Exilado: {0}",
  "welcome.join": "Bem-vindo, {0}! Código de amigo: {1} | Sala: {2}"
}
""";

    private const string DefaultPtBr = """
{
  "command.unknown": "Comando desconhecido /{0}. Digite /help para ver a lista.",
  "command.error": "Ocorreu um erro ao executar /{0}.",
  "command.usage": "Uso: /{0}",
  "command.help.list": "=== Comandos ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Comando desconhecido: /{0}",
  "command.help.aliases": "Apelidos: {0}",
  "command.note.host_only": "Somente o anfitrião pode usar /note.",
  "command.note.cleared": "Nota removida.",
  "command.note.set": "Nota definida: {0}",
  "command.color.invalid": "ID de cor inválido. Intervalo válido: 0–17.",
  "command.color.set": "Cor alterada para {0} ({1}).",
  "command.title.too_long": "Título muito longo. Máximo 12 caracteres.",
  "command.title.cleared": "Título removido.",
  "command.title.set": "Título definido: {0}",
  "command.stat.disabled": "As estatísticas de jogadores não estão ativadas neste servidor.",
  "command.stat.no_stats": "Nenhuma estatística registrada. Jogue primeiro!",
  "command.stat.header": "=== Suas estatísticas ===",
  "command.stat.games": "Jogos: {0}",
  "command.stat.wins": "Vitórias: {0}",
  "command.stat.losses": "Derrotas: {0}",
  "command.stat.impostor": "Vitórias Impostor: {0}",
  "command.stat.kills": "Assassinatos: {0}",
  "command.stat.deaths": "Mortes: {0}",
  "command.stat.tasks": "Tarefas: {0}",
  "command.stat.exiled": "Exilado: {0}",
  "welcome.join": "Bem-vindo, {0}! Código de amigo: {1} | Sala: {2}"
}
""";

    private const string DefaultEs = """
{
  "command.unknown": "Comando desconocido /{0}. Escribe /help para ver la lista.",
  "command.error": "Error al ejecutar /{0}.",
  "command.usage": "Uso: /{0}",
  "command.help.list": "=== Comandos ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Comando desconocido: /{0}",
  "command.help.aliases": "Alias: {0}",
  "command.note.host_only": "Solo el anfitrión puede usar /note.",
  "command.note.cleared": "Nota eliminada.",
  "command.note.set": "Nota establecida: {0}",
  "command.color.invalid": "ID de color inválido. Rango válido: 0–17.",
  "command.color.set": "Color cambiado a {0} ({1}).",
  "command.title.too_long": "Título demasiado largo. Máximo 12 caracteres.",
  "command.title.cleared": "Título eliminado.",
  "command.title.set": "Título establecido: {0}",
  "command.stat.disabled": "Las estadísticas de jugadores no están activadas en este servidor.",
  "command.stat.no_stats": "Aún no hay estadísticas. ¡Juega una partida primero!",
  "command.stat.header": "=== Tus estadísticas ===",
  "command.stat.games": "Partidas jugadas: {0}",
  "command.stat.wins": "Victorias: {0}",
  "command.stat.losses": "Derrotas: {0}",
  "command.stat.impostor": "Victorias impostor: {0}",
  "command.stat.kills": "Asesinatos: {0}",
  "command.stat.deaths": "Muertes: {0}",
  "command.stat.tasks": "Tareas completadas: {0}",
  "command.stat.exiled": "Exiliado: {0}",
  "welcome.join": "Bienvenido, {0}! Código de amigo: {1} | Sala: {2}"
}
""";

    private const string DefaultIt = """
{
  "command.unknown": "Comando sconosciuto /{0}. Digita /help per la lista.",
  "command.error": "Errore durante l'esecuzione di /{0}.",
  "command.usage": "Utilizzo: /{0}",
  "command.help.list": "=== Comandi ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Comando sconosciuto: /{0}",
  "command.help.aliases": "Alias: {0}",
  "command.note.host_only": "Solo l'host può usare /note.",
  "command.note.cleared": "Nota rimossa.",
  "command.note.set": "Nota impostata: {0}",
  "command.color.invalid": "ID colore non valido. Intervallo: 0–17.",
  "command.color.set": "Colore cambiato in {0} ({1}).",
  "command.title.too_long": "Titolo troppo lungo. Max 12 caratteri.",
  "command.title.cleared": "Titolo rimosso.",
  "command.title.set": "Titolo impostato: {0}",
  "command.stat.disabled": "Le statistiche dei giocatori non sono attive su questo server.",
  "command.stat.no_stats": "Nessuna statistica registrata. Gioca prima una partita!",
  "command.stat.header": "=== Le tue statistiche ===",
  "command.stat.games": "Partite giocate: {0}",
  "command.stat.wins": "Vittorie: {0}",
  "command.stat.losses": "Sconfitte: {0}",
  "command.stat.impostor": "Vittorie impostore: {0}",
  "command.stat.kills": "Uccisioni: {0}",
  "command.stat.deaths": "Morti: {0}",
  "command.stat.tasks": "Compiti completati: {0}",
  "command.stat.exiled": "Esiliato: {0}",
  "welcome.join": "Benvenuto, {0}! Codice amico: {1} | Stanza: {2}"
}
""";

    private const string DefaultNl = """
{
  "command.unknown": "Onbekend commando /{0}. Typ /help voor een lijst.",
  "command.error": "Fout bij uitvoeren van /{0}.",
  "command.usage": "Gebruik: /{0}",
  "command.help.list": "=== Commando's ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Onbekend commando: /{0}",
  "command.help.aliases": "Aliassen: {0}",
  "command.note.host_only": "Alleen de host kan /note gebruiken.",
  "command.note.cleared": "Notitie verwijderd.",
  "command.note.set": "Notitie ingesteld: {0}",
  "command.color.invalid": "Ongeldig kleur-ID. Geldig bereik: 0–17.",
  "command.color.set": "Kleur gewijzigd naar {0} ({1}).",
  "command.title.too_long": "Titel te lang. Maximaal 12 tekens.",
  "command.title.cleared": "Titel verwijderd.",
  "command.title.set": "Titel ingesteld: {0}",
  "command.stat.disabled": "Spelerstatistieken zijn niet ingeschakeld op deze server.",
  "command.stat.no_stats": "Nog geen statistieken. Speel eerst een spel!",
  "command.stat.header": "=== Jouw statistieken ===",
  "command.stat.games": "Gespeelde spellen: {0}",
  "command.stat.wins": "Overwinningen: {0}",
  "command.stat.losses": "Verliezen: {0}",
  "command.stat.impostor": "Impostor overwinningen: {0}",
  "command.stat.kills": "Moorden: {0}",
  "command.stat.deaths": "Sterfgevallen: {0}",
  "command.stat.tasks": "Taken voltooid: {0}",
  "command.stat.exiled": "Verbannen: {0}",
  "welcome.join": "Welkom, {0}! Vriendcode: {1} | Kamer: {2}"
}
""";

    private const string DefaultFil = """
{
  "command.unknown": "Hindi kilalang command /{0}. I-type ang /help para sa listahan.",
  "command.error": "May error sa pagpapatakbo ng /{0}.",
  "command.usage": "Paggamit: /{0}",
  "command.help.list": "=== Mga Command ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Hindi kilalang command: /{0}",
  "command.help.aliases": "Mga Alias: {0}",
  "command.note.host_only": "Ang host lang ang maaaring gumamit ng /note.",
  "command.note.cleared": "Natanggal ang tala.",
  "command.note.set": "Naitakda ang tala: {0}",
  "command.color.invalid": "Di-wastong color ID. Valid na hanay: 0–17.",
  "command.color.set": "Binago ang kulay sa {0} ({1}).",
  "command.title.too_long": "Masyadong mahaba ang pamagat. Max 12 karakter.",
  "command.title.cleared": "Natanggal ang pamagat.",
  "command.title.set": "Naitakda ang pamagat: {0}",
  "command.stat.disabled": "Hindi pinagana ang estadistika ng manlalaro sa server na ito.",
  "command.stat.no_stats": "Wala pang naitalang estadistika. Maglaro muna!",
  "command.stat.header": "=== Iyong Estadistika ===",
  "command.stat.games": "Mga Larong Nalaro: {0}",
  "command.stat.wins": "Mga Panalo: {0}",
  "command.stat.losses": "Mga Talo: {0}",
  "command.stat.impostor": "Mga Panalong Impostor: {0}",
  "command.stat.kills": "Mga Napatay: {0}",
  "command.stat.deaths": "Mga Kamatayan: {0}",
  "command.stat.tasks": "Mga Natapos na Gawain: {0}",
  "command.stat.exiled": "Itinapon: {0}",
  "welcome.join": "Maligayang pagdating, {0}! Friend code: {1} | Silid: {2}"
}
""";

    private const string DefaultGa = """
{
  "command.unknown": "Ordú anaithnid /{0}. Clóscríobh /help le haghaidh liosta.",
  "command.error": "Earráid agus /{0} á rith.",
  "command.usage": "Úsáid: /{0}",
  "command.help.list": "=== Orduithe ===",
  "command.help.entry": "/{0} — {1}",
  "command.help.unknown": "Ordú anaithnid: /{0}",
  "command.help.aliases": "Ailíasanna: {0}",
  "command.note.host_only": "Ní féidir ach leis an óstach /note a úsáid.",
  "command.note.cleared": "Nóta glanadh.",
  "command.note.set": "Nóta socraithe: {0}",
  "command.color.invalid": "ID datha neamhbhailí. Raon bailí: 0–17.",
  "command.color.set": "Dath athraithe go {0} ({1}).",
  "command.title.too_long": "Teideal ró-fhada. Uasmhéid 12 carachtar.",
  "command.title.cleared": "Teideal bainte.",
  "command.title.set": "Teideal socraithe: {0}",
  "command.stat.disabled": "Níl staitisticí imreoirí ar siúl ar an bhfreastalaí seo.",
  "command.stat.no_stats": "Níl aon staitisticí taifeadta fós. Imir cluiche ar dtús!",
  "command.stat.header": "=== Do Staitisticí ===",
  "command.stat.games": "Cluichí Imearthe: {0}",
  "command.stat.wins": "Buaite: {0}",
  "command.stat.losses": "Caillte: {0}",
  "command.stat.impostor": "Buaite mar Impostor: {0}",
  "command.stat.kills": "Maruithe: {0}",
  "command.stat.deaths": "Básanna: {0}",
  "command.stat.tasks": "Tascanna Críochnaithe: {0}",
  "command.stat.exiled": "Díbirthe: {0}",
  "welcome.join": "Fáilte, {0}! Cód cara: {1} | Seomra: {2}"
}
""";

}

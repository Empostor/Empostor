using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Events.Player;
using Empostor.Api.Events.Meeting;
using Empostor.Api.Games;
using Empostor.Api.Innersloth.GameOptions;
using Empostor.Api.Net.Inner.Objects;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorService
{
    private readonly NarratorConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NarratorService> _logger;
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ConcurrentDictionary<string, MeetingContext> _meetings = new();

    public NarratorService(NarratorConfig config, ILogger<NarratorService> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public void RecordGameStart(IGame game)
    {
        _games[game.Code.ToString()] = new GameState();
    }

    public void RecordGameEnd(IGame game)
    {
        _games.TryRemove(game.Code.ToString(), out _);
        _meetings.TryRemove(game.Code.ToString(), out _);
    }

    public void RecordTask(IPlayerCompletedTaskEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (!_games.TryGetValue(gameCode, out var state)) return;

        var playerName = e.PlayerControl.PlayerInfo.PlayerName;
        if (!state.PlayerTasks.TryGetValue(playerName, out var tasks))
        {
            tasks = new List<TaskRecord>();
            state.PlayerTasks[playerName] = tasks;
        }

        var taskData = e.Task.Task;
        tasks.Add(new TaskRecord
        {
            Name = taskData?.Name ?? $"Task#{e.Task.Id}",
            IsVisual = taskData?.IsVisual ?? false
        });
    }

    public void RecordMurder(IPlayerMurderEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (!_games.TryGetValue(gameCode, out var state)) return;

        var victimName = e.Victim.PlayerInfo.PlayerName;
        state.DeadPlayers.Add(victimName);

        var killerName = e.PlayerControl.PlayerInfo.PlayerName;
        state.Murders.Add(new MurderRecord { Killer = killerName, Victim = victimName });
    }

    public void RecordExile(IPlayerExileEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (!_games.TryGetValue(gameCode, out var state)) return;

        state.DeadPlayers.Add(e.PlayerControl.PlayerInfo.PlayerName);
    }

    public void RecordReport(IPlayerReportEvent e)
    {
        _logger.LogDebug("[Narrator] {reporter} reported a body", e.PlayerControl.PlayerInfo.PlayerName);
    }

    public void RecordMeetingStart(IMeetingStartedEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        var meeting = new MeetingContext
        {
            IsActive = true,
            Reporter = e.MeetingHud.Reporter?.PlayerName
        };
        _meetings[gameCode] = meeting;
    }

    public void RecordMeetingEnd(IMeetingEndedEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (_meetings.TryGetValue(gameCode, out var meeting))
            meeting.IsActive = false;
    }

    public void RecordChat(IPlayerChatEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (!_meetings.TryGetValue(gameCode, out var meeting) || !meeting.IsActive) return;

        meeting.ChatMessages.Add(new ChatMessage
        {
            PlayerName = e.PlayerControl.PlayerInfo.PlayerName,
            Message = e.Message
        });
    }

    public void RecordVote(IPlayerVotedEvent e)
    {
        var gameCode = e.Game.Code.ToString();
        if (!_meetings.TryGetValue(gameCode, out var meeting) || !meeting.IsActive) return;

        var voterName = e.PlayerControl.PlayerInfo.PlayerName;
        var votedFor = e.VotedFor?.PlayerInfo.PlayerName ?? "Skipped";
        meeting.Votes.Add(new VoteRecord { Voter = voterName, VotedFor = votedFor });
    }

    public bool IsMeetingActive(IGame game)
    {
        return _meetings.TryGetValue(game.Code.ToString(), out var meeting) && meeting.IsActive;
    }

    public string BuildContext(IGame game, IInnerPlayerControl player)
    {
        var gameCode = game.Code.ToString();
        _games.TryGetValue(gameCode, out var state);
        _meetings.TryGetValue(gameCode, out var meeting);

        var sb = new StringBuilder();
        var playerName = player.PlayerInfo.PlayerName;

        sb.AppendLine($"Your name: {playerName}");
        sb.AppendLine($"Your role: {(player.PlayerInfo.IsImpostor ? "Impostor" : "Crewmate")}");

        if (game.Options is NormalGameOptions normalOpts)
        {
            sb.AppendLine($"Visual tasks enabled: {(normalOpts.VisualTasks ? "Yes" : "No")}");
            sb.AppendLine($"Confirm ejects: {(normalOpts.ConfirmEmpostor ? "On" : "Off")}");
        }

        sb.AppendLine($"Total impostors: {game.Options.NumImpostors}");

        if (state != null && state.PlayerTasks.TryGetValue(playerName, out var tasks) && tasks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Your completed tasks:");
            foreach (var task in tasks)
            {
                var visualTag = task.IsVisual ? " [VISUAL TASK - others can see you do this!]" : "";
                sb.AppendLine($"  - {task.Name}{visualTag}");
            }
        }

        if (state != null && state.DeadPlayers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Dead players ({state.DeadPlayers.Count}):");
            foreach (var dead in state.DeadPlayers)
                sb.AppendLine($"  - {dead}");
        }

        if (meeting != null && meeting.IsActive)
        {
            sb.AppendLine();
            sb.AppendLine("--- Current Meeting ---");
            if (meeting.Reporter != null)
                sb.AppendLine($"Meeting started by: {meeting.Reporter}");

            if (meeting.ChatMessages.Count > 0)
            {
                sb.AppendLine("Chat log:");
                foreach (var msg in meeting.ChatMessages)
                    sb.AppendLine($"  {msg.PlayerName}: {msg.Message}");
            }

            if (meeting.Votes.Count > 0)
            {
                sb.AppendLine("Votes so far:");
                foreach (var v in meeting.Votes)
                    sb.AppendLine($"  {v.Voter} -> {v.VotedFor}");
            }
        }

        return sb.ToString();
    }

    public async Task<string> AskNarratorAsync(string context, string playerMessage)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
            return "Narrator is not configured. Please set your DeepSeek API key in the plugin config.";

        var systemPrompt = "You are a helpful narrator/assistant whispering advice to a player during an Among Us meeting. "
            + "You help them formulate their defense and strategy. "
            + "RULES: "
            + "1. Only use information the player would know from their own perspective. "
            + "2. If the player is Crewmate, help prove innocence — mention their visual tasks, alibis, logical arguments. "
            + "3. If the player is Impostor, help them blend in with plausible lies consistent with known info. "
            + "4. Be concise and natural, like a friend whispering advice. Keep it to 1-3 sentences. "
            + "5. Never mention you are an AI or that this is a game. Just give the advice directly.";

        try
        {
            var requestBody = new
            {
                model = _config.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Context:\n{context}\n\nThe player says: \"{playerMessage}\"\n\nGive them a short, helpful reply they can use in the meeting:" }
                },
                max_tokens = 256,
                temperature = 0.8
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _config.ApiEndpoint)
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Narrator] DeepSeek API error: {status} {body}", response.StatusCode, responseBody);
                return "Sorry, the narrator is having trouble thinking right now. Try again in a moment.";
            }

            using var doc = JsonDocument.Parse(responseBody);
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return reply?.Trim() ?? "Sorry, I couldn't come up with a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Narrator] Failed to call DeepSeek API");
            return "Sorry, the narrator encountered an error. Please try again.";
        }
    }

    private sealed class GameState
    {
        public Dictionary<string, List<TaskRecord>> PlayerTasks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DeadPlayers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MurderRecord> Murders { get; } = new();
    }

    private sealed class MeetingContext
    {
        public bool IsActive { get; set; }
        public string? Reporter { get; set; }
        public List<ChatMessage> ChatMessages { get; } = new();
        public List<VoteRecord> Votes { get; } = new();
    }

    private sealed record TaskRecord
    {
        public string Name { get; set; } = "";
        public bool IsVisual { get; set; }
    }

    private sealed record ChatMessage
    {
        public string PlayerName { get; set; } = "";
        public string Message { get; set; } = "";
    }

    private sealed record VoteRecord
    {
        public string Voter { get; set; } = "";
        public string VotedFor { get; set; } = "";
    }

    private sealed record MurderRecord
    {
        public string Killer { get; set; } = "";
        public string Victim { get; set; } = "";
    }
}

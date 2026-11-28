using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Impostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Shared
{
    /// <summary>
    /// Stores the most recent player reports in Data/reports.json.
    /// </summary>
    public sealed class ReportStore
    {
        private const int MaxReports = 500;
        private readonly object _sync = new();
        private readonly ILogger<ReportStore> _logger;
        private readonly string _path = Path.Combine("Data", "reports.json");
        private readonly List<ReportEntry> _reports = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public ReportStore(ILogger<ReportStore> logger)
        {
            _logger = logger;
            try
            {
                if (File.Exists(_path))
                {
                    _reports.AddRange(JsonSerializer.Deserialize<List<ReportEntry>>(File.ReadAllText(_path)) ?? new List<ReportEntry>());
                    if (_reports.Count > MaxReports) _reports.RemoveRange(MaxReports, _reports.Count - MaxReports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load player reports from {Path}", _path);
            }
        }

        public void Add(ReportEntry entry)
        {
            lock (_sync)
            {
                _reports.Insert(0, entry);
                if (_reports.Count > MaxReports) _reports.RemoveAt(_reports.Count - 1);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    var tempPath = _path + ".tmp";
                    File.WriteAllText(tempPath, JsonSerializer.Serialize(_reports, JsonOptions));
                    File.Move(tempPath, _path, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save player reports to {Path}", _path);
                }
            }
        }

        public List<ReportEntry> GetRecent(int count = 100)
        {
            lock (_sync) return _reports.Take(Math.Clamp(count, 0, MaxReports)).ToList();
        }

        public sealed class ReportEntry
        {
            public DateTime Time { get; init; } = DateTime.UtcNow;
            public string GameCode { get; init; } = string.Empty;
            public string ReporterName { get; init; } = string.Empty;
            public string? ReporterFriendCode { get; init; }
            public string? ReportedName { get; init; }
            public string? ReportedFriendCode { get; init; }
            public ReportReasons Reason { get; init; }
            public ReportOutcome Outcome { get; init; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Admin.Report
{
    public sealed class ReportStore : IDisposable
    {
        private static readonly string DataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        private static readonly string ReportsFile = Path.Combine(DataDir, "reports.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        private readonly ILogger<ReportStore> _logger;
        private readonly List<ReportEntry> _reports = new();
        private readonly object _lock = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private const int Max = 500;

        public ReportStore(ILogger<ReportStore> logger)
        {
            _logger = logger;
            Load();
        }

        public void Add(ReportEntry entry)
        {
            lock (_lock)
            {
                _reports.Insert(0, entry);
                if (_reports.Count > Max)
                {
                    _reports.RemoveAt(_reports.Count - 1);
                }
            }

            SaveAsync();
        }

        public List<ReportEntry> GetRecent(int count = 100)
        {
            lock (_lock)
            {
                return _reports.Take(count).ToList();
            }
        }

        private void SaveAsync()
        {
            _ = Task.Run(async () =>
            {
                if (!await _saveLock.WaitAsync(0))
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(DataDir);
                    List<ReportEntry> snapshot;
                    lock (_lock)
                    {
                        snapshot = _reports.ToList();
                    }

                    await File.WriteAllTextAsync(ReportsFile, JsonSerializer.Serialize(snapshot, JsonOpts));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Report] Failed to save reports");
                }
                finally
                {
                    _saveLock.Release();
                }
            });
        }

        private void Load()
        {
            if (!File.Exists(ReportsFile))
            {
                return;
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<ReportEntry>>(File.ReadAllText(ReportsFile));
                if (list != null)
                {
                    lock (_lock)
                    {
                        _reports.AddRange(list.Take(Max));
                    }

                    _logger.LogInformation("[Report] Loaded {Count} report(s)", _reports.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Report] Failed to load reports file");
            }
        }

        public void Dispose() => _saveLock.Dispose();
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Impostor.Server.Service.Admin.Report
{
    public sealed class ReportStore
    {
        private readonly List<ReportEntry> _reports = new();
        private readonly object _lock = new();
        private const int Max = 500;

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
        }

        public List<ReportEntry> GetRecent(int count = 100)
        {
            lock (_lock)
            {
                return _reports.Take(count).ToList();
            }
        }
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

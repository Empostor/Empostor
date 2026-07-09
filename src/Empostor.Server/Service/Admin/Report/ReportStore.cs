using System;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Admin.Report
{
    public sealed class ReportStore : JsonDataStore<List<ReportStore.ReportEntry>>
    {
        private readonly object _lock = new();
        private readonly List<ReportEntry> _reports = new();
        private const int Max = 500;

        public ReportStore(ILogger<ReportStore> logger)
            : base(logger, legacyPath: "Data/reports.json")
        {
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

            SaveFireAndForget();
        }

        public List<ReportEntry> GetRecent(int count = 100)
        {
            lock (_lock)
            {
                return _reports.Take(count).ToList();
            }
        }

        protected override List<ReportEntry> GetSnapshot()
        {
            lock (_lock)
            {
                return _reports.ToList();
            }
        }

        protected override void ApplySnapshot(List<ReportEntry> data)
        {
            lock (_lock)
            {
                _reports.Clear();
                _reports.AddRange(data.Take(Max));
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
}

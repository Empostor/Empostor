using System;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.FixedCode;

/// <summary>
///     Persistent friend-code → room-code mappings, stored in <c>Data/FixedCodeStore.json</c>.
///     The old plugin config file (<c>[Fixed Room Code]Config.json</c>) is migrated automatically.
/// </summary>
public sealed class FixedCodeStore : JsonDataStore<FixedCodeConfig>
{
    private readonly List<FriendCodeMapping> _mappings = new();

    public FixedCodeStore(ILogger<FixedCodeStore> logger)
        : base(logger, legacyPath: "[Fixed Room Code]Config.json")
    {
        Load();
    }

    public IReadOnlyList<FriendCodeMapping> Mappings => _mappings;

    public void SetMappings(IEnumerable<FriendCodeMapping> mappings)
    {
        _mappings.Clear();
        _mappings.AddRange(mappings);
        SaveFireAndForget();
    }

    public FriendCodeMapping? Find(string? friendCode)
        => _mappings.FirstOrDefault(m =>
            string.Equals(m.FriendCode, friendCode, StringComparison.OrdinalIgnoreCase));

    protected override FixedCodeConfig GetSnapshot() => new() { Mappings = _mappings.ToList() };

    protected override void ApplySnapshot(FixedCodeConfig data)
    {
        _mappings.Clear();
        _mappings.AddRange(data.Mappings);
    }
}

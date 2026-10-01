using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Api;

/// <summary>
///     Runtime HPLP settings. <c>Data/HplpData.json</c> is the only source — the admin panel reads
///     and writes it, and nothing is taken from config.json.
/// </summary>
public sealed class HplpStore : JsonDataStore<HplpConfig>
{
    public HplpStore(ILogger<HplpStore> logger)
        : base(logger, legacyPath: "hplp.json")
    {
        JsonOpts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        Load();
    }

    public bool Enabled { get; set; }

    public string RegionId { get; set; } = "default";

    public string RegionName { get; set; } = "Empostor Server";

    public string PublicUrl { get; set; } = string.Empty;

    public HplpConfig Snapshot => new()
    {
        Enabled = Enabled,
        RegionId = RegionId,
        RegionName = RegionName,
        PublicUrl = PublicUrl,
    };

    protected override HplpConfig GetSnapshot() => Snapshot;

    protected override void ApplySnapshot(HplpConfig data)
    {
        Enabled = data.Enabled;
        RegionId = data.RegionId;
        RegionName = data.RegionName;
        PublicUrl = data.PublicUrl;
    }

    public new async ValueTask SaveAsync() => await base.SaveAsync();
}

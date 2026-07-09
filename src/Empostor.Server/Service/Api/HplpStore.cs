using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Empostor.Api.Config;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Empostor.Server.Service.Api;

public sealed class HplpStore : JsonDataStore<HplpConfig>
{
    public HplpStore(ILogger<HplpStore> logger, IOptions<HplpConfig> config)
        : base(logger, legacyPath: "hplp.json")
    {
        JsonOpts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Try loading from persisted file
        Load();

        // If no persisted data, fall back to config.json defaults
        if (string.IsNullOrEmpty(RegionId) && string.IsNullOrEmpty(RegionName) && string.IsNullOrEmpty(PublicUrl))
        {
            var cfg = config.Value;
            Enabled = cfg.Enabled;
            RegionId = cfg.RegionId;
            RegionName = cfg.RegionName;
            PublicUrl = cfg.PublicUrl;
        }
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

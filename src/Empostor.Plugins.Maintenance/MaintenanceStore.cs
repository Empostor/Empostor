using System.Threading.Tasks;
using Empostor.Api.Innersloth;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Maintenance;

public sealed class MaintenanceConfig
{
    public bool Enabled { get; set; }

    public string MessageZh { get; set; } = "服务器正在维护中，请联系管理员获取支持。";

    public string MessageEn { get; set; } = "The server is under maintenance. Please contact the administrator for support.";
}

/// <summary>
///     Holds the maintenance flag and the two kick messages. Persisted so a restart does not
///     silently drop the server back into service while an operator is mid-update.
/// </summary>
public sealed class MaintenanceStore : JsonDataStore<MaintenanceConfig>
{
    public MaintenanceStore(ILogger<MaintenanceStore> logger)
        : base(logger)
    {
        Load();
    }

    public bool Enabled { get; private set; }

    public string MessageZh { get; private set; } = new MaintenanceConfig().MessageZh;

    public string MessageEn { get; private set; } = new MaintenanceConfig().MessageEn;

    /// <summary>The message shown to a client speaking the given language.</summary>
    public string MessageFor(Language language) => language switch
    {
        Language.SChinese or Language.TChinese => MessageZh,
        _ => MessageEn,
    };

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetMessages(string zh, string en)
    {
        MessageZh = zh;
        MessageEn = en;
    }

    public new async ValueTask SaveAsync() => await base.SaveAsync();

    protected override MaintenanceConfig GetSnapshot() => new()
    {
        Enabled = Enabled,
        MessageZh = MessageZh,
        MessageEn = MessageEn,
    };

    protected override void ApplySnapshot(MaintenanceConfig data)
    {
        Enabled = data.Enabled;
        MessageZh = data.MessageZh;
        MessageEn = data.MessageEn;
    }
}

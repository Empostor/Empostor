using System.Linq;
using System.Threading.Tasks;
using Empostor.Api.Service;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Maintenance;

public sealed class MaintenanceConfig
{
    private const string FallbackMessage = "服务器正在维护中，请联系管理员获取支持。";

    public bool Enabled { get; set; }

    public string? Message { get; set; }

    // Kept so a config written before the two languages were merged still loads its text.
    public string? MessageZh { get; set; }

    public string? MessageEn { get; set; }

    public string MessageOrDefault => Message ?? MessageZh ?? MessageEn ?? FallbackMessage;
}

/// <summary>
///     Holds the maintenance flag and the kick message. Persisted so a restart does not
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

    public string Message { get; private set; } = new MaintenanceConfig().MessageOrDefault;

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetMessage(string message) => Message = message;

    public new async ValueTask SaveAsync() => await base.SaveAsync();

    protected override MaintenanceConfig GetSnapshot() => new()
    {
        Enabled = Enabled,
        Message = Message,
    };

    protected override void ApplySnapshot(MaintenanceConfig data)
    {
        Enabled = data.Enabled;
        Message = FirstNonBlank(data.Message, data.MessageZh, data.MessageEn)
                  ?? new MaintenanceConfig().MessageOrDefault;
    }

    private static string? FirstNonBlank(params string?[] candidates)
        => candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
}

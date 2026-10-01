using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class WebhookOrphanCleanup : IHostedService
{
    private readonly LiveGameTracker _tracker;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebhookOrphanCleanup> _logger;

    public WebhookOrphanCleanup(
        LiveGameTracker tracker,
        IHttpClientFactory http,
        ILogger<WebhookOrphanCleanup> logger)
    {
        _tracker = tracker;
        _http = http;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var orphans = _tracker.All.ToList();
        if (orphans.Count == 0)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "DiscordWebhook deleting {Count} orphaned lobby message(s) left over from the previous run", orphans.Count);
        _tracker.Clear();

        _ = Task.Run(async () =>
        {
            using var client = _http.CreateClient();
            foreach (var lobby in orphans)
            {
                try
                {
                    using var response = await client.DeleteAsync(
                        $"{lobby.WebhookUrl.TrimEnd('/')}/messages/{lobby.MessageId}");
                    _logger.LogInformation(
                        "DiscordWebhook orphan cleanup for {Code}: {Status}",
                        lobby.GameCodeName, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DiscordWebhook orphan cleanup failed for {Code}", lobby.GameCodeName);
                }
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

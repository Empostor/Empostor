using System;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Events;
using Empostor.Api.Events.Client;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Titles.Service;

public sealed class FriendCodeTitleListener : IEventListener
{
    private readonly ILogger<FriendCodeTitleListener> _logger;
    private readonly TitlesConfig _config;
    private Dictionary<string, string> _map;

    public FriendCodeTitleListener(
        ILogger<FriendCodeTitleListener> logger,
        TitlesConfig config)
    {
        _logger = logger;
        _config = config;
        _map = BuildMap();

        _logger.LogInformation("[FriendCodeTitles] Loaded {Count} title mapping(s).", _map.Count);
    }

    /// <summary>
    ///     Reloads the title mappings from config. Called by the API middleware
    ///     after a new title is added.
    /// </summary>
    public void Reload()
    {
        _map = BuildMap();
        _logger.LogInformation("[FriendCodeTitles] Reloaded {Count} title mapping(s).", _map.Count);
    }

    [EventListener]
    public void OnClientConnected(IClientConnectedEvent e)
    {
        var fc = e.Client.FriendCode;
        if (string.IsNullOrEmpty(fc)) return;
        if (!_map.TryGetValue(fc, out var title)) return;

        try
        {
            var displayName = TitleStore.BuildDisplayName(title, e.Client.Name);
            e.Client.Name = displayName;

            _logger.LogInformation("[FriendCodeTitles] Applied [{Title}] to {Name} ({FC})",
                title, e.Client.Name, fc);

            // Remove applied title so it is only used once
            _map.Remove(fc);

            // Also remove from config so it persists across reloads
            var entry = _config.Titles.FirstOrDefault(t =>
                string.Equals(t.FriendCode, fc, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                _config.Titles.Remove(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FriendCodeTitles] Failed to apply title for {FC}", fc);
        }
    }

    private Dictionary<string, string> BuildMap()
    {
        return _config.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.FriendCode) && !string.IsNullOrWhiteSpace(t.Title))
            .ToDictionary(t => t.FriendCode, t => t.Title, StringComparer.OrdinalIgnoreCase);
    }
}

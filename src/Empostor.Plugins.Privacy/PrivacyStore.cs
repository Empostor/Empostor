using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Empostor.Api.Admin;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Privacy;

public sealed class PrivacyStore
{
    private static readonly string PagesDir = Path.Combine(Directory.GetCurrentDirectory(), "Pages");
    private static readonly string PrivacyFile = Path.Combine(PagesDir, "privacy.html");

    private readonly ILogger<PrivacyStore> _logger;
    private bool _initialized;
    private readonly object _initLock = new();

    public PrivacyStore(ILogger<PrivacyStore> logger)
    {
        _logger = logger;
    }

    public string GetContent()
    {
        EnsureFile();
        if (!File.Exists(PrivacyFile))
        {
            return DefaultPrivacyHtml();
        }

        return File.ReadAllText(PrivacyFile);
    }

    public void SaveContent(string content)
    {
        EnsureFile();
        File.WriteAllText(PrivacyFile, content);
        _logger.LogInformation("Privacy policy updated.");
    }

    private void EnsureFile()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (!Directory.Exists(PagesDir))
            {
                Directory.CreateDirectory(PagesDir);
            }

            if (!File.Exists(PrivacyFile))
            {
                File.WriteAllText(PrivacyFile, DefaultPrivacyHtml());
                _logger.LogInformation("Written default Pages/privacy.html");
            }
        }
    }

    private static string DefaultPrivacyHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Privacy Policy - Empostor Server</title>
<style>
  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; max-width: 800px; margin: 0 auto; padding: 2rem; line-height: 1.6; color: #333; }
  h1 { border-bottom: 2px solid #eee; padding-bottom: 0.5rem; }
  h2 { margin-top: 2rem; }
</style>
</head>
<body>
<h1>Privacy Policy</h1>
<p>Last updated: 2026-05-23</p>

<h2>1. Data Collection</h2>
<p>This is a private Among Us server. We collect the minimum data required for the game to function:</p>
<ul>
  <li><strong>Friend codes</strong> — used for player identification and game invitations.</li>
  <li><strong>Chat messages</strong> — processed in-game and may be logged for moderation purposes.</li>
  <li><strong>IP addresses</strong> — used for network connectivity and may be logged for security.</li>
  <li><strong>Gameplay data</strong> — tasks completed, kills, votes, and other in-game actions.</li>
</ul>

<h2>2. Data Usage</h2>
<p>Collected data is used solely for:</p>
<ul>
  <li>Providing the game service</li>
  <li>Moderation and anti-cheat enforcement</li>
  <li>Service improvement and debugging</li>
</ul>

<h2>3. Data Sharing</h2>
<p>We do not sell or share your personal data with third parties. Data may be disclosed if required by law.</p>

<h2>4. Data Retention</h2>
<p>Game logs are retained for up to 31 days. Player statistics are retained while your account is active.</p>

<h2>5. Contact</h2>
<p>If you have questions about this privacy policy, contact the server administrator.</p>

<h2>6. Third-Party Services</h2>
<p>This server may use third-party plugins that process data. Each plugin operates under its own privacy terms.</p>
<p>In-game chat filters are applied for content moderation purposes.</p>
</body>
</html>
""";
}

using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Http;

[Route("/")]
public sealed class HelloController : ControllerBase
{
    private static readonly string PagesDir = Path.Combine(Directory.GetCurrentDirectory(), "Pages");
    private static readonly string IndexFile = Path.Combine(PagesDir, "index.html");
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    private readonly ILogger<HelloController> _logger;

    public HelloController(ILogger<HelloController> logger)
    {
        _logger = logger;
        EnsurePagesDirectory();
    }

    [HttpGet]
    public IActionResult GetIndex()
    {
        if (!System.IO.File.Exists(IndexFile))
        {
            _logger.LogWarning("[HelloController] Pages/index.html not found, serving fallback.");
            return Content(FallbackHtml(), "text/html; charset=utf-8");
        }

        var html = System.IO.File.ReadAllText(IndexFile);
        return Content(html, "text/html; charset=utf-8");
    }

    private void EnsurePagesDirectory()
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
                _logger.LogInformation("[HelloController] Created Pages/ directory at {Path}", PagesDir);
            }

            if (!System.IO.File.Exists(IndexFile))
            {
                System.IO.File.WriteAllText(IndexFile, DefaultIndexHtml());
                _logger.LogInformation("[HelloController] Written default Pages/index.html");
            }
        }
    }

    private static string DefaultIndexHtml() => $"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Empostor Server</title>
</head>
<body>
<div class="card">
  <div class="dot"></div>
  <h1>Empostor is running</h1>
  <p>Your private Among Us server is online and accepting connections.</p>
  <div class="links">
    <a class="link-btn" href="https://github.com/Empostor/Empostor" target="_blank">🌐Github Repo</a>
    <a class="link-btn" href="https://impostor.github.io/Impostor" target="_blank">🌐 Generate Region File</a>
  </div>
</div>
<footer>Started 2026-05-15 16:16 UTC &nbsp;·&nbsp; Edit this page at Pages/index.html</footer>
</body>
</html>
""";

    private static string FallbackHtml() =>
        "<html><h2>Empostor is running</h2>" +
        "<p>Pages/index.html was deleted. Restart the server to regenerate it.</p>" +
        "<p><a href='/admin'>Admin Panel</a></p></body></html>";
}

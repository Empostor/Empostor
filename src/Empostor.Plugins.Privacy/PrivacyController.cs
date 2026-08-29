using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Empostor.Plugins.Privacy;

[ApiController]
[Route("/privacy")]
public sealed class PrivacyController : ControllerBase
{
    private readonly PrivacyStore _store;
    private readonly ILogger<PrivacyController> _logger;

    public PrivacyController(PrivacyStore store, ILogger<PrivacyController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetPrivacy()
    {
        var html = _store.GetContent();
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    [Route("/admin/api/privacy")]
    public async Task<IActionResult> UpdatePrivacy()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        try
        {
            var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("content").GetString() ?? string.Empty;
            var token = doc.RootElement.GetProperty("token").GetString() ?? string.Empty;

            var adminToken = Environment.GetEnvironmentVariable("EMP_HTTP_TOKEN")
                          ?? Environment.GetEnvironmentVariable("EMP_ADMIN_TOKEN")
                          ?? "empostor";

            if (token != adminToken)
            {
                return Unauthorized(new { error = "Invalid token." });
            }

            _store.SaveContent(content);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

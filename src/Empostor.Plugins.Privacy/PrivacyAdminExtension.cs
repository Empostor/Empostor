using System.Text.Json;
using Empostor.Api.Admin;

namespace Empostor.Plugins.Privacy;

public sealed class PrivacyAdminExtension : IAdminExtension
{
    private readonly PrivacyStore _store;

    public PrivacyAdminExtension(PrivacyStore store)
    {
        _store = store;
    }

    public string Id => "privacy-policy";

    public string Title => "Privacy Policy";

    public string Icon => "shield";

    public string Section => "Server";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterTextbox(t =>
        {
            t.Label = "Privacy Policy HTML";
            t.Value = _store.GetContent();
            t.Multiline = true;
            t.SubmitOnChange = true;
            t.OnSubmit(async ctx =>
            {
                _store.SaveContent(ctx.Value ?? string.Empty);
                return AdminActionResult.Ok("Privacy policy saved.");
            });
        });

        b.RegisterText(txt =>
        {
            txt.Content = "The privacy policy is served at /privacy. Edit the HTML above to update it.";
            txt.Tone = "muted";
        });
    }
}

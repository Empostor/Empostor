using Empostor.Api.Admin;

namespace Empostor.Plugins.FriendCodeValidator;

public sealed class FriendCodeValidatorAdminExtension : IAdminExtension
{
    public string Id => "friend-code-validator";

    public string Title => "Friend Code Validator";

    public string Icon => "filter";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterText(
            "Enforces a valid friend code on every join: a word plus a 4-digit suffix (word#0000).",
            "default");
        b.RegisterText(
            "Format: ^([a-zA-Z]+)#(\\d{4})$, with the word split into an (A, B) or (B, A) pair from the built-in dictionaries.",
            "muted",
            monospace: true);
        b.RegisterText(
            "This plugin has no configurable options — the rules and dictionaries are compiled in.",
            "muted");
    }
}

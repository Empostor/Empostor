using Empostor.Api.Admin;

namespace Empostor.Plugin.Code;

public sealed class CodeAdminExtension : IAdminExtension
{
    private readonly IGameCodeManager _manager;

    public CodeAdminExtension(IGameCodeManager manager)
    {
        _manager = manager;
    }

    public string Id => "game-codes";

    public string Title => "Game Codes";

    public string Icon => "database";

    public void Build(AdminPanelBuilder b)
    {
        b.RegisterText(
            $"Loaded {_manager.SixCharCodes} six-character and {_manager.FourCharCodes} four-character code(s).",
            "default");
        b.RegisterText($"Source directory: {_manager.Path}", "muted", monospace: true);
        b.RegisterText(
            "Codes are read from .txt files at startup. Add or edit files in the directory, then restart the server to reload.",
            "muted");
    }
}

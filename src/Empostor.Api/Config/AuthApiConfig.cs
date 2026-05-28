namespace Empostor.Api.Config;

public enum AuthApiMode
{
    Innersloth,
    Niko,
    Both,
    Relay,
    Ume,
}

public class AuthApiConfig
{
    public const string Section = "AuthApi";

    public AuthApiMode Mode { get; set; } = AuthApiMode.Innersloth;

    public string NikoApiKey { get; set; } = "";

    public string NikoApiBaseUrl { get; set; } = "https://au-verify.niko233.top";

    public string RelayApiBaseUrl { get; set; } = "http://localhost:5100";

    public string RelayApiKey { get; set; } = "";

    public string UmeApiBaseUrl { get; set; } = "https://auverify.hayashiume.top";

    public string UmeApiKey { get; set; } = "sk-empostor-globalapikey";
}

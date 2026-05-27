namespace Empostor.Api.Config;

public enum AuthApiMode
{
    Innersloth,
    Niko,
    Both,
}

public class AuthApiConfig
{
    public const string Section = "AuthApi";

    public AuthApiMode Mode { get; set; } = AuthApiMode.Innersloth;

    public string NikoApiKey { get; set; } = "";

    public string NikoApiBaseUrl { get; set; } = "https://au-verify.niko233.top";
}

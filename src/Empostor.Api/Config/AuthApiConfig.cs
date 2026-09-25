namespace Empostor.Api.Config;

public enum AuthApiMode
{
    Innersloth,
    Both,
    Ume,
}

public class AuthApiConfig
{
    public const string Section = "AuthApi";

    public AuthApiMode Mode { get; set; } = AuthApiMode.Innersloth;

    public string UmeApiBaseUrl { get; set; } = "https://auverify.hayashiume.top";

    public string UmeApiKey { get; set; } = "sk-empostor-globalapikey";
}

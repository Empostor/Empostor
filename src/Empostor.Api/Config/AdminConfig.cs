namespace Empostor.Api.Config
{
    public class AdminConfig
    {
        public const string Section = "Admin";

        public string Password { get; set; } = string.Empty;

        public string MarketplaceUrl { get; set; } =
            "https://raw.githubusercontent.com/Empostor/Empostor/main/marketplace/plugins.json";
    }
}

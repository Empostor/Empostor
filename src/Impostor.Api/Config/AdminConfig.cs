namespace Impostor.Api.Config
{
    public class AdminConfig
    {
        public const string Section = "Admin";

        public string Password { get; set; } = "admin123";

        public string MarketplaceUrl { get; set; } =
            "https://raw.githubusercontent.com/Empostor/Empostor/main/marketplace/plugins.json";
    }
}

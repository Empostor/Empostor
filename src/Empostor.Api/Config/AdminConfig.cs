namespace Empostor.Api.Config
{
    public class AdminConfig
    {
        public const string Section = "Admin";

        public string Password { get; set; } = string.Empty;

        public string MarketplaceUrl { get; set; } =
            "https://raw.githubusercontent.com/Empostor/Empostor/main/marketplace/plugins.json";

        /// <summary>Active admin theme id. Themes live under <c>Pages/themes/{Id}</c> or as plugins.</summary>
        public string Theme { get; set; } = "default";

        /// <summary>Initial color mode ("light" or "dark"). Operators can still toggle it in the panel.</summary>
        public string ThemeMode { get; set; } = "dark";
    }
}

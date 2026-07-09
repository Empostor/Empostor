namespace Empostor.Api.Config
{
    /// <summary>
    ///     Configuration for the HPLP (HTTP Public Lobby List Protocol) endpoint,
    ///     which allows Starlight clients to discover games hosted on this server.
    /// </summary>
    public class HplpConfig
    {
        public const string Section = "HPLP";

        /// <summary>
        ///     Whether the HPLP endpoint (<c>GET /x-api/games</c>) is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        ///     Region identifier reported to Starlight clients (HPLP <c>region_id</c>).
        /// </summary>
        public string RegionId { get; set; } = "default";

        /// <summary>
        ///     Human-readable region name displayed in the Starlight lobby UI.
        /// </summary>
        public string RegionName { get; set; } = "Empostor Server";

        /// <summary>
        ///     Public URL used by Starlight clients to connect to this server.
        ///     When empty, the URL is auto-constructed as <c>http://{PublicIp}:{HttpPort}</c>.
        ///     Set this to a custom value (e.g. <c>https://your-domain.com</c>) when
        ///     running behind a reverse proxy with TLS.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;
    }
}

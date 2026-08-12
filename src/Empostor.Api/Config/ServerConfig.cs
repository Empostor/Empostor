using Empostor.Api.Utils;

namespace Empostor.Api.Config
{
    public class ServerConfig
    {
        public const string Section = "Server";

        private string? _resolvedPublicIp;
        private string? _resolvedListenIp;

        public string PublicIp { get; set; } = "127.0.0.1";

        public ushort PublicPort { get; set; } = 22023;

        public string ListenIp { get; set; } = "0.0.0.0";

        public ushort ListenPort { get; set; } = 22023;

        public bool UseUfw { get; set; } = false;

        public bool UseFirewalld { get; set; } = false;

        public ushort DeltaPortStart { get; set; } = 0;

        public ushort DeltaPortEnd { get; set; } = 0;

        public string ResolvePublicIp()
        {
            return _resolvedPublicIp ??= IpUtils.ResolveIp(PublicIp);
        }

        public string ResolveListenIp()
        {
            return _resolvedListenIp ??= IpUtils.ResolveIp(ListenIp);
        }
    }
}

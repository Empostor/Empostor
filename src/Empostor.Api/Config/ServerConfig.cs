using System;
using System.Collections.Generic;
using Empostor.Api.Utils;

namespace Empostor.Api.Config
{
    // How the HTTP authentication (POST /api/user) session is tied to the UDP connection.
    public enum ServerVerificationMode
    {
        // Allocates a unique dynamic UDP port per player and uses it as a nonce.
        DeltaPort = 0,

        // Matches connections by a disposable fake GameCode issued per join, without dynamic ports.
        GameCode = 1,
    }

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

        // DeltaPort (default) or GameCode.
        public ServerVerificationMode VerificationMode { get; set; } = ServerVerificationMode.DeltaPort;

        // Ports used in GameCode mode. Empty falls back to ListenPort; only the first one is bound.
        public List<int> GameCodePorts { get; set; } = new();

        // Seconds to keep join/host requests pending; UDP connections arriving in this window are matched against them.
        public int TemporaryGameCodeLifetimeSeconds { get; set; } = 180;

        public ushort DeltaPortStart { get; set; } = 0;

        public ushort DeltaPortEnd { get; set; } = 0;

        public ushort DeltaPortLowWaterMark { get; set; } = 5;

        public bool UseGameCodeMode => VerificationMode == ServerVerificationMode.GameCode;

        public ushort GameCodeListenPort => GameCodePorts.Count > 0 ? (ushort)GameCodePorts[0] : ListenPort;

        public ushort GameCodePublicPort => GameCodePorts.Count > 0 ? (ushort)GameCodePorts[0] : PublicPort;

        public TimeSpan TemporaryGameCodeLifetime =>
            TimeSpan.FromSeconds(Math.Max(1, TemporaryGameCodeLifetimeSeconds));

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

using System;
using System.Collections.Generic;
using Impostor.Api.Utils;

namespace Impostor.Api.Config
{
    public enum ServerVerificationMode
    {
        DeltaPort = 0,
        GameCode = 1,
        Disable = 2,
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

        public bool UseUfw { get; set; }

        public bool UseFirewalld { get; set; }

        public ServerVerificationMode VerificationMode { get; set; } = ServerVerificationMode.DeltaPort;

        public List<int> GameCodePorts { get; set; } = new();

        public int TemporaryGameCodeLifetimeSeconds { get; set; } = 180;

        public ushort DeltaPortStart { get; set; }

        public ushort DeltaPortEnd { get; set; }

        public ushort DeltaPortLowWaterMark { get; set; } = 5;

        public bool UseDeltaPortMode => VerificationMode == ServerVerificationMode.DeltaPort;

        public bool UseGameCodeMode => VerificationMode == ServerVerificationMode.GameCode;

        public bool IsVerificationDisabled => VerificationMode == ServerVerificationMode.Disable;

        public ushort GameCodeListenPort => GameCodePorts.Count > 0 ? checked((ushort)GameCodePorts[0]) : ListenPort;

        public ushort GameCodePublicPort => GameCodePorts.Count > 0 ? checked((ushort)GameCodePorts[0]) : PublicPort;

        public TimeSpan TemporaryGameCodeLifetime => TimeSpan.FromSeconds(Math.Max(1, TemporaryGameCodeLifetimeSeconds));

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

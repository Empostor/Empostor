using System;
using System.Collections.Generic;
using Empostor.Api.Utils;

namespace Empostor.Api.Config
{
    // HTTP 認証 (POST /api/user) と UDP 接続をどう結びつけるか。
    public enum ServerVerificationMode
    {
        // プレイヤーごとにユニークな動的 UDP ポートを払い出し、それを nonce 代わりにする。
        DeltaPort = 0,

        // 動的ポートを使わず、加入ごとの使い捨て偽 GameCode で対応付ける。
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

        // DeltaPort（既定）か GameCode。
        public ServerVerificationMode VerificationMode { get; set; } = ServerVerificationMode.DeltaPort;

        // GameCode モードで使うポート。空なら ListenPort。先頭の 1 つだけバインドする。
        public List<int> GameCodePorts { get; set; } = new();

        // 加入/募集要求を保留する秒数。この間に届いた UDP 接続と突き合わせる。
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

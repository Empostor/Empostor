using Empostor.Api.Innersloth;

namespace Empostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// Deserializes a UDP handshake packet.
        ///
        /// Actual UDP (non-DTLS) handshake packet layout (based on Among Us client and preview project):
        ///   GameVersion (4 bytes)
        ///   Name (string)
        ///   [V1+] LastNonce / uint32 (4 bytes) — ignored by server
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message) + ProductUserId (string, client-reported) + CrossplayFlags (uint32)
        ///
        /// Note: UDP handshake packets do not include matchmakerToken or friendCode.
        /// Authentication data is exchanged beforehand via the HTTP /api/user endpoint
        /// and the TCP/UDP matching is done via dynamic delta port allocation (nonce).
        /// </summary>
        public static void Deserialize(
            IMessageReader reader,
            out GameVersion clientVersion,
            out string name,
            out Language language,
            out QuickChatModes chatMode,
            out PlatformSpecificData? platformSpecificData)
        {
            clientVersion = reader.ReadGameVersion();
            name = reader.ReadString();

            // V1+: lastNonce / lastId (uint32) — used by client for reconnection, ignored by server
            if (clientVersion >= Version.V1)
            {
                reader.ReadUInt32();
            }

            // V2+: language + chat mode
            if (clientVersion >= Version.V2)
            {
                language = (Language)reader.ReadUInt32();
                chatMode = (QuickChatModes)reader.ReadByte();
            }
            else
            {
                language = Language.English;
                chatMode = QuickChatModes.FreeChatOrQuickChat;
            }

            // V3+: platform data (message) + ProductUserId (string) + CrossplayFlags (uint32)
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);

                // ProductUserId (string) — ignored, we use the port-based matching
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadString(); } catch { /* ignore */ }
                }

                // CrossplayFlags (uint32)
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* ignore */ }
                }
            }
            else
            {
                platformSpecificData = null;
            }
        }

        private static class Version
        {
            public static readonly GameVersion V1 = new(2021, 4, 25);
            public static readonly GameVersion V2 = new(2021, 6, 30);
            public static readonly GameVersion V3 = new(2021, 11, 9);
        }
    }
}

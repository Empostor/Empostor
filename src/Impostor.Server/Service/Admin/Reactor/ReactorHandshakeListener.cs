using System;
using System.Collections.Generic;
using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service.Admin.Reactor
{
    // Based on Reactor.Impostor protocol:
    // https://github.com/NuclearPowered/Reactor.Impostor
    //
    // Reactor appends its data after the normal Among Us handshake:
    // [normal AU handshake] [uint64 header: 7-byte "reactor" magic + 1-byte protocol version]
    // If protocol >= V3: [packed int modCount] [for each: string id, string version, ushort flags]
    internal sealed class ReactorHandshakeListener : IEventListener
    {
        // "reactor" in ASCII (7 bytes), packed as upper 56 bits of a uint64 in little-endian
        private const ulong ReactorMagic = 0x726f7463656172;

        private readonly ILogger<ReactorHandshakeListener> _logger;

        public ReactorHandshakeListener(ILogger<ReactorHandshakeListener> logger)
            => _logger = logger;

        [EventListener]
        public void OnClientConnection(IClientConnectionEvent e)
        {
            var reader = e.HandshakeData;
            var savedPos = reader.Position;

            try
            {
                // Skip known Among Us handshake fields to reach the Reactor suffix
                if (!SkipAuHandshake(reader))
                {
                    return;
                }

                // Scan for Reactor header in remaining bytes
                if (!TryFindReactorHeader(reader, out var protocolVersion))
                {
                    if (reader.Position < reader.Length)
                    {
                        _logger.LogTrace("[ReactorMods] No Reactor header found for {Name}", e.Connection.Client?.Name ?? "unknown");
                    }

                    return;
                }

                // Read mod list for protocol V3+
                var mods = Array.Empty<ClientMod>();
                if (protocolVersion >= 3)
                {
                    mods = ReadModList(reader);
                }

                var info = new ReactorModInfo($"v{protocolVersion}", mods);
                e.Connection.SetReactorMods(info);

                if (mods.Length > 0)
                {
                    _logger.LogInformation(
                        "[ReactorMods] {Name} has {Count} mod(s) (protocol v{Proto}): {Mods}",
                        e.Connection.Client?.Name ?? "unknown",
                        mods.Length,
                        protocolVersion,
                        string.Join(", ", System.Linq.Enumerable.Select(mods, m => $"{m.Id} {m.Version}")));
                }
            }
            catch
            {
                // Not a Reactor client or parse error — silently skip
            }
            finally
            {
                reader.Seek(savedPos);
            }
        }

        private static bool SkipAuHandshake(IMessageReader reader)
        {
            // GameVersion (int32, 4 bytes)
            if (!CanRead(reader, 4))
            {
                return false;
            }

            reader.ReadInt32();

            // Name (Hazel string: packed int length + UTF-8 bytes)
            if (!TrySkipString(reader))
            {
                return false;
            }

            // LastNonce (uint32, 4 bytes)
            if (!CanRead(reader, 4))
            {
                return false;
            }

            reader.ReadUInt32();

            // Language (uint32, 4 bytes)
            if (!CanRead(reader, 4))
            {
                return false;
            }

            reader.ReadUInt32();

            // ChatMode (byte)
            if (!CanRead(reader, 1))
            {
                return false;
            }

            reader.ReadByte();

            // PlatformSpecificData (Hazel sub-message)
            if (!TrySkipMessage(reader))
            {
                return false;
            }

            return true;
        }

        // Scan remaining bytes for the Reactor header magic
        private static bool TryFindReactorHeader(IMessageReader reader, out int protocolVersion)
        {
            protocolVersion = 0;

            while (reader.Position + 8 <= reader.Length)
            {
                var pos = reader.Position;
                var value = reader.ReadUInt64();
                var magic = value >> 8;

                if (magic == ReactorMagic)
                {
                    protocolVersion = unchecked((byte)(value & 0xFF));
                    return true;
                }

                // Not found at this position, advance by 1 byte and retry
                reader.Seek(pos + 1);
            }

            return false;
        }

        private static ClientMod[] ReadModList(IMessageReader reader)
        {
            if (reader.Position >= reader.Length)
            {
                return Array.Empty<ClientMod>();
            }

            var modCount = reader.ReadPackedInt32();
            var mods = new ClientMod[modCount];

            for (var i = 0; i < modCount; i++)
            {
                var id = reader.ReadString();
                var version = reader.ReadString();
                var flags = reader.ReadUInt16();
                var requiredOnAll = (flags & 0x01) != 0;

                // When RequireOnAllClients flag is set, an extra name string follows
                if (requiredOnAll && reader.Position < reader.Length)
                {
                    _ = reader.ReadString();
                }

                mods[i] = new ClientMod(id, version, requiredOnAll);
            }

            return mods;
        }

        private static bool CanRead(IMessageReader reader, int count)
            => reader.Position + count <= reader.Length;

        private static bool TrySkipString(IMessageReader reader)
        {
            if (!CanRead(reader, 1))
            {
                return false;
            }

            var pos = reader.Position;
            var len = reader.ReadPackedInt32();

            if (len < 0 || reader.Position + len > reader.Length)
            {
                reader.Seek(pos);
                return false;
            }

            reader.Seek(reader.Position + len);
            return true;
        }

        private static bool TrySkipMessage(IMessageReader reader)
        {
            if (!CanRead(reader, 2))
            {
                return false;
            }

            var pos = reader.Position;
            var len = reader.ReadUInt16();

            if (reader.Position + len > reader.Length)
            {
                reader.Seek(pos);
                return false;
            }

            reader.Seek(reader.Position + len);
            return true;
        }
    }

    public sealed class ReactorModInfo
    {
        public ReactorModInfo(string protocolVersion, IReadOnlyList<ClientMod> mods)
        {
            ProtocolVersion = protocolVersion;
            Mods = mods;
        }

        public string ProtocolVersion { get; }

        public IReadOnlyList<ClientMod> Mods { get; }
    }

    public sealed class ClientMod
    {
        public ClientMod(string id, string version, bool requiredOnAllClients)
        {
            Id = id;
            Version = version;
            RequiredOnAllClients = requiredOnAllClients;
        }

        public string Id { get; }

        public string Version { get; }

        public bool RequiredOnAllClients { get; }
    }
}

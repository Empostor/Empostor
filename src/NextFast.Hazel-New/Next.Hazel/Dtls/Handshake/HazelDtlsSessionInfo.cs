using System;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/Decode session information in ClientHello
/// </summary>
public struct HazelDtlsSessionInfo
{
    public const byte CurrentClientSessionSize = 1;
    public const byte CurrentClientSessionVersion = 1;

    public byte FullSize => (byte)(1 + PayloadSize);
    public byte PayloadSize;
    public byte Version;

    public HazelDtlsSessionInfo(byte version)
    {
        Version = version;
        switch (version)
        {
            case 0: // Does not write version byte
                PayloadSize = 0;
                return;
            case 1: // Writes version byte only
                PayloadSize = 1;
                return;
        }

        throw new ArgumentOutOfRangeException(nameof(version));
    }

    public void Encode(ByteSpan writer)
    {
        writer[0] = PayloadSize;

        if (Version > 0) writer[1] = Version;
    }

    public static bool Parse(out HazelDtlsSessionInfo result, ByteSpan reader)
    {
        result = new HazelDtlsSessionInfo();
        if (reader.Length < 1) return false;

        result.PayloadSize = reader[0];

        // Back compat, length may be zero, version defaults to 0.
        if (result.PayloadSize == 0)
        {
            result.Version = 0;
            return true;
        }

        // Forward compat, if length > 1, ignore the rest
        result.Version = reader[1];
        return true;
    }
}
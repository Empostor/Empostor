using System.Diagnostics;
using Next.Hazel.Dtls.Handshake.Constant;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/decode Handshake ServerHello message
/// </summary>
public struct ServerHello
{
    public ProtocolVersion ServerProtocolVersion;
    public ByteSpan Random;
    public CipherSuite CipherSuite;
    public HazelDtlsSessionInfo Session;

    public const int MinSize = 0
                               + 2 // server_version
                               + Constant.Random.Size // random
                               + 1 // session_id (size)
                               + 2 // cipher_suite
                               + 1 // compression_method
        ;

    public int Size => MinSize + Session.PayloadSize;

    /// <summary>
    ///     Parse a Handshake ServerHello payload from wire format
    /// </summary>
    /// <returns>
    ///     True if we successfully decode the ServerHello
    ///     message. Otherwise false.
    /// </returns>
    public static bool Parse(out ServerHello result, ByteSpan span)
    {
        result = new ServerHello();
        if (span.Length < MinSize) return false;

        result.ServerProtocolVersion = (ProtocolVersion)span.ReadBigEndian16();
        span = span[2..];

        result.Random = span[..Constant.Random.Size];
        span = span[Constant.Random.Size..];

        if (!HazelDtlsSessionInfo.Parse(out result.Session, span)) return false;

        span = span[result.Session.FullSize..];

        result.CipherSuite = (CipherSuite)span.ReadBigEndian16();
        span = span[2..];

        var compressionMethod = (CompressionMethod)span[0];
        return compressionMethod == CompressionMethod.Null;
    }

    /// <summary>
    ///     Encode Handshake ServerHello to wire format
    /// </summary>
    public void Encode(ByteSpan span)
    {
        Debug.Assert(Random.Length == Constant.Random.Size);

        span.WriteBigEndian16((ushort)ServerProtocolVersion);
        span = span[2..];

        Random.CopyTo(span);
        span = span[Constant.Random.Size..];

        Session.Encode(span);
        span = span[Session.FullSize..];

        span.WriteBigEndian16((ushort)CipherSuite);
        span = span[2..];

        span[0] = (byte)CompressionMethod.Null;
    }
}
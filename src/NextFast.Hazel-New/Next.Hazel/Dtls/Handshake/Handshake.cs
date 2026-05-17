using Next.Hazel.Dtls.Handshake.Constant;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/decode handshake protocol header
/// </summary>
public struct Handshake
{
    public HandshakeType MessageType;
    public uint Length;
    public ushort MessageSequence;
    public uint FragmentOffset;
    public uint FragmentLength;

    public const int Size = 12;

    /// <summary>
    ///     Parse a Handshake protocol header from wire format
    /// </summary>
    /// <returns>True if we successfully decode a handshake header. Otherwise false</returns>
    public static bool Parse(out Handshake header, ByteSpan span)
    {
        header = new Handshake();

        if (span.Length < Size) return false;

        header.MessageType = (HandshakeType)span[0];
        header.Length = span.ReadBigEndian24(1);
        header.MessageSequence = span.ReadBigEndian16(4);
        header.FragmentOffset = span.ReadBigEndian24(6);
        header.FragmentLength = span.ReadBigEndian24(9);
        return true;
    }

    /// <summary>
    ///     Encode the Handshake protocol header to wire format
    /// </summary>
    /// <param name="span"></param>
    public void Encode(ByteSpan span)
    {
        span[0] = (byte)MessageType;
        span.WriteBigEndian24(Length, 1);
        span.WriteBigEndian16(MessageSequence, 4);
        span.WriteBigEndian24(FragmentOffset, 6);
        span.WriteBigEndian24(FragmentLength, 9);
    }
}
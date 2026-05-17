using System.Diagnostics;
using Next.Hazel.Dtls.Handshake.Constant;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/decode ClientHello Handshake message
/// </summary>
public struct ClientHello
{
    public ProtocolVersion ClientProtocolVersion;
    public HazelDtlsSessionInfo SessionInfo;
    public ByteSpan Random;
    public ByteSpan Cookie;
    public ByteSpan CipherSuites;
    public ByteSpan SupportedCurves;

    public const int MinSize = 0
                               + 2 // client_version
                               + Constant.Random.Size // random
                               + 1 // session_id (size)
                               + 1 // cookie (size)
                               + 2 // cipher_suites (size)
                               + 1 // compression_methods (size)
                               + 1 // compression_method[0] (NULL)
                               + 2 // extensions size
                               + 0 // NamedCurveList extensions[0]
                               + 2 // extensions[0].extension_type
                               + 2 // extensions[0].extension_data (length)
                               + 2 // extensions[0].named_curve_list (size)
        ;

    /// <summary>
    ///     Calculate the size in bytes required for the ClientHello payload
    /// </summary>
    /// <returns></returns>
    public int CalculateSize()
    {
        return MinSize
               + SessionInfo.PayloadSize
               + Cookie.Length
               + CipherSuites.Length
               + SupportedCurves.Length
            ;
    }

    /// <summary>
    ///     Parse a Handshake ClientHello payload from wire format
    /// </summary>
    /// <returns>True if we successfully decode the ClientHello message. Otherwise false</returns>
    public static bool Parse(out ClientHello result, ProtocolVersion? expectedProtocolVersion, ByteSpan span)
    {
        result = new ClientHello();
        if (span.Length < MinSize) return false;

        result.ClientProtocolVersion = (ProtocolVersion)span.ReadBigEndian16();
        if (expectedProtocolVersion.HasValue && result.ClientProtocolVersion != expectedProtocolVersion.Value)
            return false;
        span = span[2..];

        result.Random = span[..Constant.Random.Size];
        span = span[Constant.Random.Size..];

        if (!HazelDtlsSessionInfo.Parse(out result.SessionInfo, span)) return false;

        span = span[result.SessionInfo.FullSize..];

        var cookieSize = span[0];
        if (span.Length < 1 + cookieSize) return false;
        result.Cookie = span.Slice(1, cookieSize);
        span = span[(1 + cookieSize)..];

        var cipherSuiteSize = span.ReadBigEndian16();
        if (span.Length < 2 + cipherSuiteSize) return false;

        if (cipherSuiteSize % 2 != 0) return false;
        result.CipherSuites = span.Slice(2, cipherSuiteSize);
        span = span[(2 + cipherSuiteSize)..];

        int compressionMethodsSize = span[0];
        var foundNullCompressionMethod = false;
        for (var ii = 0; ii != compressionMethodsSize; ++ii)
            if (span[1 + ii] == (byte)CompressionMethod.Null)
            {
                foundNullCompressionMethod = true;
                break;
            }

        if (!foundNullCompressionMethod
            || span.Length < 1 + compressionMethodsSize)
            return false;

        span = span[(1 + compressionMethodsSize)..];

        switch (span.Length)
        {
            // Parse extensions
            case <= 0:
                return true;
            case < 2:
                return false;
        }

        var extensionsSize = span.ReadBigEndian16();
        span = span[2..];
        if (span.Length != extensionsSize) return false;

        while (span.Length > 0)
        {
            // Parse extension header
            if (span.Length < 4) return false;

            var extensionType = (ExtensionType)span.ReadBigEndian16();
            var extensionLength = span.ReadBigEndian16(2);

            if (span.Length < 4 + extensionLength) return false;

            var extensionData = span.Slice(4, extensionLength);
            span = span[(4 + extensionLength)..];
            result.ParseExtension(extensionType, extensionData);
        }

        return true;
    }

    /// <summary>
    ///     Decode a ClientHello extension
    /// </summary>
    /// <param name="extensionType">Extension type</param>
    /// <param name="extensionData">Extension data</param>
    private void ParseExtension(ExtensionType extensionType, ByteSpan extensionData)
    {
        switch (extensionType)
        {
            case ExtensionType.EllipticCurves:
                if (extensionData.Length % 2 != 0) break;

                if (extensionData.Length < 2) break;

                var namedCurveSize = extensionData.ReadBigEndian16();
                if (namedCurveSize % 2 != 0) break;

                SupportedCurves = extensionData.Slice(2, namedCurveSize);
                break;
        }
    }

    /// <summary>
    ///     Determines if the ClientHello message advertises support
    ///     for the specified cipher suite
    /// </summary>
    public bool ContainsCipherSuite(CipherSuite cipherSuite)
    {
        var iterator = CipherSuites;
        while (iterator.Length >= 2)
        {
            if (iterator.ReadBigEndian16() == (ushort)cipherSuite) return true;

            iterator = iterator[2..];
        }

        return false;
    }

    /// <summary>
    ///     Determines if the ClientHello message advertises support
    ///     for the specified curve
    /// </summary>
    public bool ContainsCurve(NamedCurve curve)
    {
        var iterator = SupportedCurves;
        while (iterator.Length >= 2)
        {
            if (iterator.ReadBigEndian16() == (ushort)curve) return true;

            iterator = iterator[2..];
        }

        return false;
    }

    /// <summary>
    ///     Encode Handshake ClientHello payload to wire format
    /// </summary>
    public void Encode(ByteSpan span)
    {
        span.WriteBigEndian16((ushort)ProtocolVersion.DTLS1_2);
        span = span[2..];

        Debug.Assert(Random.Length == Constant.Random.Size);
        Random.CopyTo(span);
        span = span[Constant.Random.Size..];


        SessionInfo.Encode(span);
        span = span[SessionInfo.FullSize..];

        span[0] = (byte)Cookie.Length;
        Cookie.CopyTo(span[1..]);
        span = span[(1 + Cookie.Length)..];

        span.WriteBigEndian16((ushort)CipherSuites.Length);
        CipherSuites.CopyTo(span[2..]);
        span = span[(2 + CipherSuites.Length)..];

        span[0] = 1;
        span[1] = (byte)CompressionMethod.Null;
        span = span[2..];

        // Extensions size
        span.WriteBigEndian16((ushort)(6 + SupportedCurves.Length));
        span = span[2..];

        // Supported curves extension
        span.WriteBigEndian16((ushort)ExtensionType.EllipticCurves);
        span.WriteBigEndian16((ushort)(2 + SupportedCurves.Length), 2);
        span.WriteBigEndian16((ushort)SupportedCurves.Length, 4);
        SupportedCurves.CopyTo(span[6..]);
    }
}
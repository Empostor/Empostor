using System.Net;
using System.Security.Cryptography;
using Next.Hazel.Crypto;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/decode Handshake HelloVerifyRequest message
/// </summary>
public struct HelloVerifyRequest
{
    public const int CookieSize = 20;

    public const int Size = 0
                            + 2 // server_version
                            + 1 // cookie (size)
                            + CookieSize // cookie (data)
        ;

    public ProtocolVersion ServerProtocolVersion;
    public ByteSpan Cookie;

    /// <summary>
    ///     Parse a Handshake HelloVerifyRequest payload from wire
    ///     format
    /// </summary>
    /// <returns>
    ///     True if we successfully decode the HelloVerifyRequest
    ///     message. Otherwise false.
    /// </returns>
    public static bool Parse(out HelloVerifyRequest result, ProtocolVersion? expectedProtocolVersion, ByteSpan span)
    {
        result = new HelloVerifyRequest();
        if (span.Length < 3) return false;

        result.ServerProtocolVersion = (ProtocolVersion)span.ReadBigEndian16();
        if (expectedProtocolVersion.HasValue && result.ServerProtocolVersion != expectedProtocolVersion.Value)
            return false;


        var cookieSize = span[2];
        span = span[3..];

        if (span.Length < cookieSize) return false;

        result.Cookie = span;
        return true;
    }

    /// <summary>
    ///     Encode a HelloVerifyRequest payload to wire format
    /// </summary>
    /// <param name="span"></param>
    /// <param name="peerAddress">Address of the remote peer</param>
    /// <param name="hmac">Listener HMAC signature provider</param>
    /// <param name="protocolVersion"></param>
    public static void Encode(ByteSpan span, EndPoint peerAddress, HMAC hmac, ProtocolVersion protocolVersion)
    {
        var cookie = ComputeAddressMac(peerAddress, hmac);

        span.WriteBigEndian16((ushort)protocolVersion);
        span[2] = CookieSize;
        cookie.CopyTo(span[3..]);
    }

    /// <summary>
    ///     Generate an HMAC for a peer address
    /// </summary>
    /// <param name="peerAddress">Address of the remote peer</param>
    /// <param name="hmac">Listener HMAC signature provider</param>
    public static ByteSpan ComputeAddressMac(EndPoint peerAddress, HMAC hmac)
    {
        var address = peerAddress.Serialize();
        var data = new byte[address.Size];
        for (int ii = 0, nn = data.Length; ii != nn; ++ii) data[ii] = address[ii];

        ByteSpan signature = hmac.ComputeHash(data);
        return signature[..CookieSize];
    }

    /// <summary>
    ///     Verify a client's cookie was signed by our listener
    /// </summary>
    /// <param name="cookie">Wire format cookie</param>
    /// <param name="peerAddress">Address of the remote peer</param>
    /// <param name="hmac">Listener HMAC signature provider</param>
    /// <returns>True if the cookie is valid. Otherwise false</returns>
    public static bool VerifyCookie(ByteSpan cookie, EndPoint peerAddress, HMAC hmac)
    {
        if (cookie.Length != CookieSize) return false;

        var expectedHash = ComputeAddressMac(peerAddress, hmac);
        if (expectedHash.Length != cookie.Length) return false;

        return 1 == Const.ConstantCompareSpans(cookie, expectedHash);
    }
}
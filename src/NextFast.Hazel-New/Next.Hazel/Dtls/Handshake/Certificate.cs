using System;
using System.Security.Cryptography.X509Certificates;

namespace Next.Hazel.Dtls.Handshake;

/// <summary>
///     Encode/decode Handshake Certificate message
/// </summary>
public struct Certificate
{
    /// <summary>
    ///     Encode a certificate to wire formate
    /// </summary>
    public static ByteSpan Encode(X509Certificate2 certificate)
    {
        ByteSpan certData = certificate.GetRawCertData();
        var totalSize = certData.Length + 3 + 3;

        ByteSpan result = new byte[totalSize];

        var writer = result;
        writer.WriteBigEndian24((uint)certData.Length + 3);
        writer = writer[3..];
        writer.WriteBigEndian24((uint)certData.Length);
        writer = writer[3..];

        certData.CopyTo(writer);
        return result;
    }

    /// <summary>
    ///     Parse a Handshake Certificate payload from wire format
    /// </summary>
    /// <returns>True if we successfully decode the Certificate message. Otherwise false</returns>
    public static bool Parse(out X509Certificate2 certificate, ByteSpan span)
    {
        certificate = null;
        if (span.Length < 6) return false;

        var totalSize = span.ReadBigEndian24();
        span = span[3..];

        if (span.Length < totalSize) return false;

        var certificateSize = span.ReadBigEndian24();
        span = span[3..];
        if (span.Length < certificateSize) return false;

        var rawData = new byte[certificateSize];
        span.CopyTo(rawData, 0);
        try
        {
#if NET8_0 || NET6_0
            certificate = new X509Certificate2(rawData);
#else
            certificate = X509CertificateLoader.LoadCertificate(rawData);
#endif
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Next.Hazel;

// https://github.com/willardf/Hazel-Networking/blob/main/Hazel.UnitTests/Dtls/DtlsConnectionTests.cs
public static class DtlsHelper
{
    public static X509Certificate2 GetCertificate(string certificateSting, string privateKeyString = "")
    {
        var privateKey = privateKeyString != "" ? DecodeRSAKeyFromPEM(privateKeyString) : null;
        var rawData = DecodePEM(certificateSting);
#if NET8_0 || NET6_0
        var certificate = new X509Certificate2(rawData);
#else
        var certificate = X509CertificateLoader.LoadCertificate(rawData);
#endif
        return privateKey != null
            ? certificate.CopyWithPrivateKey(privateKey)
            : certificate;
    }

    public static X509Certificate2Collection GetCertificateCollection(this X509Certificate2 certificate)
    {
        return new X509Certificate2Collection(certificate);
    }

    public static byte[] DecodePEM(string pemData)
    {
        var result = new List<byte>();

        var lines = pemData.Replace("\r", "").Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("-----")) continue;

            var lineData = Convert.FromBase64String(line);
            result.AddRange(lineData);
        }

        return result.ToArray();
    }

    public static RSA DecodeRSAKeyFromPEM(string pemData)
    {
        var pemReader = new PemReader(new StringReader(pemData));
        var parameters = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)pemReader.ReadObject());
        var rsa = RSA.Create();
        rsa.ImportParameters(parameters);
        return rsa;
    }
}
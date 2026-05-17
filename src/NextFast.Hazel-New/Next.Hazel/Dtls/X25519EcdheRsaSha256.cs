using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Next.Hazel.Crypto;
using Next.Hazel.Dtls.Handshake.Constant;
using HashAlgorithm = Next.Hazel.Dtls.Handshake.Constant.HashAlgorithm;

namespace Next.Hazel.Dtls;

/// <summary>
///     ECDHE_RSA_*_256 cipher suite
/// </summary>
public class X25519EcdheRsaSha256 : IHandshakeCipherSuite
{
    private static readonly int ClientMessageSize = 0
                                                    + 1 + X25519.KeySize // ECPoint ClientKeyExchange.ecdh_Yc
        ;

    private readonly ByteSpan privateAgreementKey;
    private SHA256 sha256 = SHA256.Create();

    /// <summary>
    ///     Create a new instance of the x25519 key exchange
    /// </summary>
    /// <param name="random">Random data source</param>
    public X25519EcdheRsaSha256(RandomNumberGenerator random)
    {
        var buffer = new byte[X25519.KeySize];
        random.GetBytes(buffer);
        privateAgreementKey = buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        sha256?.Dispose();
        sha256 = null;
    }

    /// <inheritdoc />
    public int SharedKeySize()
    {
        return X25519.KeySize;
    }

    /// <inheritdoc />
    public int CalculateServerMessageSize(object privateKey)
    {
        if (privateKey is not RSA rsaPrivateKey) throw new ArgumentException("Invalid private key", nameof(privateKey));

        return CalculateServerMessageSize(rsaPrivateKey.KeySize);
    }

    /// <inheritdoc />
    public void EncodeServerKeyExchangeMessage(ByteSpan output, object privateKey)
    {
        var rsaPrivateKey = privateKey as RSA;
        if (rsaPrivateKey == null) throw new ArgumentException("Invalid private key", nameof(privateKey));

        output[0] = (byte)ECCurveType.NamedCurve;
        output.WriteBigEndian16((ushort)NamedCurve.x25519, 1);
        output[3] = X25519.KeySize;
        X25519.Func(output.Slice(4, X25519.KeySize), privateAgreementKey);

        // Hash the key parameters
        var paramterDigest = sha256.ComputeHash(output.GetUnderlyingArray(), output.Offset, 4 + X25519.KeySize);

        // Sign the paramter digest
        var signer = new RSAPKCS1SignatureFormatter(rsaPrivateKey);
        signer.SetHashAlgorithm("SHA256");
        ByteSpan signature = signer.CreateSignature(paramterDigest);

        Debug.Assert(signature.Length == rsaPrivateKey.KeySize / 8);
        output[4 + X25519.KeySize] = (byte)HashAlgorithm.Sha256;
        output[5 + X25519.KeySize] = (byte)SignatureAlgorithm.RSA;
        output[(6 + X25519.KeySize)..].WriteBigEndian16((ushort)signature.Length);
        signature.CopyTo(output[(8 + X25519.KeySize)..]);
    }

    /// <inheritdoc />
    public bool VerifyServerMessageAndGenerateSharedKey(ByteSpan output, ByteSpan serverKeyExchangeMessage,
        object publicKey)
    {
        if (publicKey is not RSA rsaPublicKey) return false;

        if (output.Length != X25519.KeySize) return false;

        // Verify message is compatible with this cipher suite
        if (serverKeyExchangeMessage.Length != CalculateServerMessageSize(rsaPublicKey.KeySize)) return false;

        if (serverKeyExchangeMessage[0] != (byte)ECCurveType.NamedCurve) return false;

        if (serverKeyExchangeMessage.ReadBigEndian16(1) != (ushort)NamedCurve.x25519) return false;

        if (serverKeyExchangeMessage[3] != X25519.KeySize) return false;

        if (serverKeyExchangeMessage[4 + X25519.KeySize] != (byte)HashAlgorithm.Sha256) return false;

        if (serverKeyExchangeMessage[5 + X25519.KeySize] != (byte)SignatureAlgorithm.RSA) return false;

        var keyParameters = serverKeyExchangeMessage[..(4 + X25519.KeySize)];
        var othersPublicKey = keyParameters[4..];
        var signatureSize = serverKeyExchangeMessage.ReadBigEndian16(6 + X25519.KeySize);
        var signature = serverKeyExchangeMessage[(4 + keyParameters.Length)..];

        if (signatureSize != signature.Length) return false;

        // Hash the key parameters
        var parameterDigest =
            sha256.ComputeHash(keyParameters.GetUnderlyingArray(), keyParameters.Offset, keyParameters.Length);

        // Verify the signature
        var verifier = new RSAPKCS1SignatureDeformatter(rsaPublicKey);
        verifier.SetHashAlgorithm("SHA256");
        if (!verifier.VerifySignature(parameterDigest, signature.ToArray())) return false;

        // Signature has been validated, generate the shared key
        return X25519.Func(output, privateAgreementKey, othersPublicKey);
    }

    /// <inheritdoc />
    public int CalculateClientMessageSize()
    {
        return ClientMessageSize;
    }

    /// <inheritdoc />
    public void EncodeClientKeyExchangeMessage(ByteSpan output)
    {
        output[0] = X25519.KeySize;
        X25519.Func(output.Slice(1, X25519.KeySize), privateAgreementKey);
    }

    /// <inheritdoc />
    public bool VerifyClientMessageAndGenerateSharedKey(ByteSpan output, ByteSpan clientKeyExchangeMessage)
    {
        if (clientKeyExchangeMessage.Length != ClientMessageSize) return false;

        if (clientKeyExchangeMessage[0] != X25519.KeySize) return false;

        var othersPublicKey = clientKeyExchangeMessage[1..];
        return X25519.Func(output, privateAgreementKey, othersPublicKey);
    }

    /// <summary>
    ///     Calculate the server message size given an RSA key size
    /// </summary>
    /// <param name="keySize">
    ///     Size of the private key (in bits)
    /// </param>
    /// <returns>
    ///     Size of the ServerKeyExchange message in bytes
    /// </returns>
    private static int CalculateServerMessageSize(int keySize)
    {
        var signatureSize = keySize / 8;

        return 0
               + 1 // ECCurveType ServerKeyExchange.params.curve_params.curve_type
               + 2 // NamedCurve ServerKeyExchange.params.curve_params.namedcurve
               + 1 + X25519.KeySize // ECPoint ServerKeyExchange.params.public
               + 1 // HashAlgorithm ServerKeyExchange.algorithm.hash
               + 1 // SignatureAlgorithm ServerKeyExchange.signed_params.algorithm.signature
               + 2 // ServerKeyExchange.signed_params.size
               + signatureSize // ServerKeyExchange.signed_params.opaque
            ;
    }
}
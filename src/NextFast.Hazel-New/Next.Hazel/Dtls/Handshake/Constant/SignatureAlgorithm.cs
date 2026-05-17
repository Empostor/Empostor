namespace Next.Hazel.Dtls.Handshake.Constant;

/// <summary>
///     Signature algorithms
/// </summary>
public enum SignatureAlgorithm : byte
{
    Anonymous = 0,
    RSA = 1,
    ECDSA = 3
}
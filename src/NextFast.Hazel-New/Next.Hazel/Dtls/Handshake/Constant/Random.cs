namespace Next.Hazel.Dtls.Handshake.Constant;

/// <summary>
///     Random state for entropy
/// </summary>
public struct Random
{
    public const int Size = 0
                            + 4 // gmt_unix_time
                            + 28 // random_bytes
        ;
}
using System.Diagnostics;
using Next.Hazel.Crypto;

namespace Next.Hazel.Dtls;

/// <summary>
///     *_AES_128_GCM_* cipher suite
/// </summary>
public class Aes128GcmRecordProtection : IRecordProtection
{
    private const int ImplicitNonceSize = 4;
    private const int ExplicitNonceSize = 8;
    private readonly Aes128Gcm clientWriteCipher;
    private readonly ByteSpan clientWriteIV;

    private readonly Aes128Gcm serverWriteCipher;

    private readonly ByteSpan serverWriteIV;

    /// <summary>
    ///     Create a new instance of the AES128_GCM record protection
    /// </summary>
    /// <param name="masterSecret">Shared secret</param>
    /// <param name="serverRandom">Server random data</param>
    /// <param name="clientRandom">Client random data</param>
    public Aes128GcmRecordProtection(ByteSpan masterSecret, ByteSpan serverRandom, ByteSpan clientRandom)
    {
        ByteSpan combinedRandom = new byte[serverRandom.Length + clientRandom.Length];
        serverRandom.CopyTo(combinedRandom);
        clientRandom.CopyTo(combinedRandom[serverRandom.Length..]);

        // Expand master_secret to encryption keys
        const int ExpandedSize = 0
                                 + 0 // mac_key_length
                                 + 0 // mac_key_length
                                 + Aes128Gcm.KeySize // enc_key_length
                                 + Aes128Gcm.KeySize // enc_key_length
                                 + ImplicitNonceSize // fixed_iv_length
                                 + ImplicitNonceSize // fixed_iv_length
            ;

        ByteSpan expandedKey = new byte[ExpandedSize];
        PrfSha256.ExpandSecret(expandedKey, masterSecret, PrfLabel.KEY_EXPANSION, combinedRandom);

        var clientWriteKey = expandedKey[..Aes128Gcm.KeySize];
        var serverWriteKey = expandedKey.Slice(Aes128Gcm.KeySize, Aes128Gcm.KeySize);
        clientWriteIV = expandedKey.Slice(2 * Aes128Gcm.KeySize, ImplicitNonceSize);
        serverWriteIV = expandedKey.Slice(2 * Aes128Gcm.KeySize + ImplicitNonceSize, ImplicitNonceSize);

        serverWriteCipher = new Aes128Gcm(serverWriteKey);
        clientWriteCipher = new Aes128Gcm(clientWriteKey);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        serverWriteCipher.Dispose();
        clientWriteCipher.Dispose();
    }

    /// <inheritdoc />
    public int GetEncryptedSize(int dataSize)
    {
        return GetEncryptedSizeImpl(dataSize);
    }

    /// <inheritdoc />
    public int GetDecryptedSize(int dataSize)
    {
        return GetDecryptedSizeImpl(dataSize);
    }

    /// <inheritdoc />
    public void EncryptServerPlaintext(ByteSpan output, ByteSpan input, ref Record record)
    {
        EncryptPlaintext(output, input, ref record, serverWriteCipher, serverWriteIV);
    }

    /// <inheritdoc />
    public void EncryptClientPlaintext(ByteSpan output, ByteSpan input, ref Record record)
    {
        EncryptPlaintext(output, input, ref record, clientWriteCipher, clientWriteIV);
    }

    /// <inheritdoc />
    public bool DecryptCiphertextFromServer(ByteSpan output, ByteSpan input, ref Record record)
    {
        return DecryptCiphertext(output, input, ref record, serverWriteCipher, serverWriteIV);
    }

    /// <inheritdoc />
    public bool DecryptCiphertextFromClient(ByteSpan output, ByteSpan input, ref Record record)
    {
        return DecryptCiphertext(output, input, ref record, clientWriteCipher, clientWriteIV);
    }


    private static int GetEncryptedSizeImpl(int dataSize)
    {
        return dataSize + Aes128Gcm.CiphertextOverhead;
    }

    private static int GetDecryptedSizeImpl(int dataSize)
    {
        return dataSize - Aes128Gcm.CiphertextOverhead;
    }

    private static void EncryptPlaintext(ByteSpan output, ByteSpan input, ref Record record, Aes128Gcm cipher,
        ByteSpan writeIV)
    {
        Debug.Assert(output.Length >= GetEncryptedSizeImpl(input.Length));

        // Build GCM nonce (authenticated data)
        ByteSpan nonce = new byte[ImplicitNonceSize + ExplicitNonceSize];
        writeIV.CopyTo(nonce);
        nonce.WriteBigEndian16(record.Epoch, ImplicitNonceSize);
        nonce.WriteBigEndian48(record.SequenceNumber, ImplicitNonceSize + 2);

        // Serialize record as additional data
        var plaintextRecord = record;
        plaintextRecord.Length = (ushort)input.Length;
        ByteSpan associatedData = new byte[Record.Size];
        plaintextRecord.Encode(associatedData);

        cipher.Seal(output, nonce, input, associatedData);
    }

    private static bool DecryptCiphertext(ByteSpan output, ByteSpan input, ref Record record, Aes128Gcm cipher,
        ByteSpan writeIV)
    {
        Debug.Assert(output.Length >= GetDecryptedSizeImpl(input.Length));

        // Build GCM nonce (authenticated data)
        ByteSpan nonce = new byte[ImplicitNonceSize + ExplicitNonceSize];
        writeIV.CopyTo(nonce);
        nonce.WriteBigEndian16(record.Epoch, ImplicitNonceSize);
        nonce.WriteBigEndian48(record.SequenceNumber, ImplicitNonceSize + 2);

        // Serialize record as additional data
        var plaintextRecord = record;
        plaintextRecord.Length = (ushort)GetDecryptedSizeImpl(input.Length);
        ByteSpan associatedData = new byte[Record.Size];
        plaintextRecord.Encode(associatedData);

        return cipher.Open(output, nonce, input, associatedData);
    }
}
using System;
using System.Security.Cryptography;

namespace Next.Hazel.Crypto;

/// <summary>
///     AES provider using the default System.Security.Cryptography implementation
/// </summary>
public class DefaultAes : IAes
{
    private readonly ICryptoTransform encryptor_;

    /// <summary>
    ///     Create a new default instance of the AES block cipher
    /// </summary>
    /// <param name="key">Encryption key</param>
    public DefaultAes(ByteSpan key)
    {
        // Create the AES block cipher
        using (var aes = Aes.Create())
        {
            aes.KeySize = key.Length * 8;
            aes.BlockSize = aes.KeySize;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.Zeros;
            aes.Key = key.ToArray();

            encryptor_ = aes.CreateEncryptor();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        encryptor_.Dispose();
    }

    /// <inheritdoc />
    public int EncryptBlock(ByteSpan inputSpan, ByteSpan outputSpan)
    {
        if (inputSpan.Length != outputSpan.Length)
            throw new ArgumentException(
                $"ouputSpan length ({outputSpan.Length}) does not match inputSpan length ({inputSpan.Length})",
                nameof(outputSpan));

        return encryptor_.TransformBlock(inputSpan.GetUnderlyingArray(), inputSpan.Offset, inputSpan.Length,
            outputSpan.GetUnderlyingArray(), outputSpan.Offset);
    }
}
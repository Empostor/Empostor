using System;
using System.Security.Cryptography;
using System.Text;

namespace Empostor.Server.Http;

internal static class AdminAuthHelper
{
    /// <summary>
    ///     Used so the plaintext password never enters a cookie or lingers in memory.
    /// </summary>
    internal static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Uses CryptographicOperations.FixedTimeEquals to prevent timing side-channel attacks.
    /// </summary>
    internal static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }
}

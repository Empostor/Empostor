using System;
using System.Security.Cryptography;
using System.Text;

namespace Empostor.Server.Http;

/// <summary>
/// Shared admin authentication utilities: SHA256 hashing and constant-time comparison.
/// </summary>
internal static class AdminAuthHelper
{
    /// <summary>
    /// Computes the hex-encoded SHA256 hash of the input string.
    /// Used so the plaintext password never enters a cookie or lingers in memory.
    /// </summary>
    internal static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing side-channel attacks.
    /// Uses CryptographicOperations.FixedTimeEquals on the UTF-8 byte representations.
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

using System;
using System.Security.Cryptography;

namespace Empostor.Plugin.Code;

public static class StrongRandom
{
    public static int Next(int minValue, int maxExclusiveValue)
    {
        if (minValue >= maxExclusiveValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be lower than maxExclusiveValue");

        var range = (long)maxExclusiveValue - minValue;
        var limit = (long)((ulong.MaxValue / (ulong)range) * (ulong)range);

        uint value;
        do
        {
            value = GetUint();
        }
        while ((ulong)value >= (ulong)limit);

        return (int)(minValue + (long)((ulong)value % (ulong)range));
    }

    private static uint GetUint()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt32(bytes);
    }
}

using System;
using System.Diagnostics;

namespace Next.Hazel.Crypto;

/// <summary>
///     The x25519 key agreement algorithm
/// </summary>
public static class X25519
{
    public const int KeySize = 32;

    private static readonly byte[] BasePoint =
        { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    ///     Performs the core x25519 function: Multiplying an EC point by a scalar value
    /// </summary>
    public static bool Func(ByteSpan output, ByteSpan scalar, ByteSpan point)
    {
        InternalFunc(output, scalar, point);
        if (Const.ConstantCompareZeroSpan(output) == 1) return false;

        return true;
    }

    /// <summary>
    ///     Multiplies the base x25519 point by the provided scalar value
    /// </summary>
    public static void Func(ByteSpan output, ByteSpan scalar)
    {
        InternalFunc(output, scalar, BasePoint);
    }

    // The FieldElement code below is ported from the original
    // public domain reference implemtation of X25519
    // by D. J. Bernstien
    //
    // See: https://cr.yp.to/ecdh.html

    private static void InternalFunc(ByteSpan output, ByteSpan scalar, ByteSpan point)
    {
        if (output.Length != KeySize) throw new ArgumentException("Invalid output size", nameof(output));

        if (scalar.Length != KeySize) throw new ArgumentException("Invalid scalar size", nameof(scalar));

        if (point.Length != KeySize) throw new ArgumentException("Invalid point size", nameof(point));

        // copy the scalar so we can properly mask it
        ByteSpan maskedScalar = new byte[32];
        scalar.CopyTo(maskedScalar);
        maskedScalar[0] &= 248;
        maskedScalar[31] &= 127;
        maskedScalar[31] |= 64;

        var x1 = FieldElement.FromBytes(point);
        var x2 = FieldElement.One();
        var x3 = x1;
        var z2 = FieldElement.Zero();
        var z3 = FieldElement.One();

        var tmp0 = new FieldElement();
        var tmp1 = new FieldElement();

        var swap = 0;
        for (var pos = 254; pos >= 0; --pos)
        {
            var b = maskedScalar[pos / 8] >> (pos % 8);
            b &= 1;
            swap ^= b;

            FieldElement.ConditionalSwap(ref x2, ref x3, swap);
            FieldElement.ConditionalSwap(ref z2, ref z3, swap);
            swap = b;

            FieldElement.Sub(ref tmp0, ref x3, ref z3);
            FieldElement.Sub(ref tmp1, ref x2, ref z2);
            FieldElement.Add(ref x2, ref x2, ref z2);
            FieldElement.Add(ref z2, ref x3, ref z3);
            FieldElement.Multiply(ref z3, ref tmp0, ref x2);
            FieldElement.Multiply(ref z2, ref z2, ref tmp1);
            FieldElement.Square(ref tmp0, ref tmp1);
            FieldElement.Square(ref tmp1, ref x2);
            FieldElement.Add(ref x3, ref z3, ref z2);
            FieldElement.Sub(ref z2, ref z3, ref z2);
            FieldElement.Multiply(ref x2, ref tmp1, ref tmp0);
            FieldElement.Sub(ref tmp1, ref tmp1, ref tmp0);
            FieldElement.Square(ref z2, ref z2);
            FieldElement.Multiply121666(ref z3, ref tmp1);
            FieldElement.Square(ref x3, ref x3);
            FieldElement.Add(ref tmp0, ref tmp0, ref z3);
            FieldElement.Multiply(ref z3, ref x1, ref z2);
            FieldElement.Multiply(ref z2, ref tmp1, ref tmp0);
        }

        FieldElement.ConditionalSwap(ref x2, ref x3, swap);
        FieldElement.ConditionalSwap(ref z2, ref z3, swap);

        FieldElement.Invert(ref z2, ref z2);
        FieldElement.Multiply(ref x2, ref x2, ref z2);
        x2.CopyTo(output);
    }

    /// <summary>
    ///     Element in the GF(2^255 - 19) field
    /// </summary>
    public partial struct FieldElement
    {
        public int x0, x1, x2, x3, x4;
        public int x5, x6, x7, x8, x9;
    }


    /// <summary>
    ///     Mathematical operators over GF(2^255 - 19)
    /// </summary>
    partial struct FieldElement
    {
        /// <summary>
        ///     Convert a byte array to a field element
        /// </summary>
        public static FieldElement FromBytes(ByteSpan bytes)
        {
            Debug.Assert(bytes.Length >= KeySize);

            var tmp0 = (long)bytes.ReadLittleEndian32();
            var tmp1 = (long)bytes.ReadLittleEndian24(4) << 6;
            var tmp2 = (long)bytes.ReadLittleEndian24(7) << 5;
            var tmp3 = (long)bytes.ReadLittleEndian24(10) << 3;
            var tmp4 = (long)bytes.ReadLittleEndian24(13) << 2;
            var tmp5 = (long)bytes.ReadLittleEndian32(16);
            var tmp6 = (long)bytes.ReadLittleEndian24(20) << 7;
            var tmp7 = (long)bytes.ReadLittleEndian24(23) << 5;
            var tmp8 = (long)bytes.ReadLittleEndian24(26) << 4;
            var tmp9 = (long)(bytes.ReadLittleEndian24(29) & 0x007FFFFF) << 2;

            var carry9 = (tmp9 + (1L << 24)) >> 25;
            tmp0 += carry9 * 19;
            tmp9 -= carry9 << 25;
            var carry1 = (tmp1 + (1L << 24)) >> 25;
            tmp2 += carry1;
            tmp1 -= carry1 << 25;
            var carry3 = (tmp3 + (1L << 24)) >> 25;
            tmp4 += carry3;
            tmp3 -= carry3 << 25;
            var carry5 = (tmp5 + (1L << 24)) >> 25;
            tmp6 += carry5;
            tmp5 -= carry5 << 25;
            var carry7 = (tmp7 + (1L << 24)) >> 25;
            tmp8 += carry7;
            tmp7 -= carry7 << 25;

            var carry0 = (tmp0 + (1L << 25)) >> 26;
            tmp1 += carry0;
            tmp0 -= carry0 << 26;
            var carry2 = (tmp2 + (1L << 25)) >> 26;
            tmp3 += carry2;
            tmp2 -= carry2 << 26;
            var carry4 = (tmp4 + (1L << 25)) >> 26;
            tmp5 += carry4;
            tmp4 -= carry4 << 26;
            var carry6 = (tmp6 + (1L << 25)) >> 26;
            tmp7 += carry6;
            tmp6 -= carry6 << 26;
            var carry8 = (tmp8 + (1L << 25)) >> 26;
            tmp9 += carry8;
            tmp8 -= carry8 << 26;

            return new FieldElement
            {
                x0 = (int)tmp0,
                x1 = (int)tmp1,
                x2 = (int)tmp2,
                x3 = (int)tmp3,
                x4 = (int)tmp4,
                x5 = (int)tmp5,
                x6 = (int)tmp6,
                x7 = (int)tmp7,
                x8 = (int)tmp8,
                x9 = (int)tmp9
            };
        }

        /// <summary>
        ///     Convert the field element to a byte array
        /// </summary>
        public void CopyTo(ByteSpan output)
        {
            Debug.Assert(output.Length >= 32);

            var q = (19 * x9 + (1L << 24)) >> 25;
            q = (x0 + q) >> 26;
            q = (x1 + q) >> 25;
            q = (x2 + q) >> 26;
            q = (x3 + q) >> 25;
            q = (x4 + q) >> 26;
            q = (x5 + q) >> 25;
            q = (x6 + q) >> 26;
            q = (x7 + q) >> 25;
            q = (x8 + q) >> 26;
            q = (x9 + q) >> 25;

            x0 = (int)(x0 + 19L * q);

            var carry0 = x0 >> 26;
            x1 = x1 + carry0;
            x0 = x0 - (carry0 << 26);
            var carry1 = x1 >> 25;
            x2 = x2 + carry1;
            x1 = x1 - (carry1 << 25);
            var carry2 = x2 >> 26;
            x3 = x3 + carry2;
            x2 = x2 - (carry2 << 26);
            var carry3 = x3 >> 25;
            x4 = x4 + carry3;
            x3 = x3 - (carry3 << 25);
            var carry4 = x4 >> 26;
            x5 = x5 + carry4;
            x4 = x4 - (carry4 << 26);
            var carry5 = x5 >> 25;
            x6 = x6 + carry5;
            x5 = x5 - (carry5 << 25);
            var carry6 = x6 >> 26;
            x7 = x7 + carry6;
            x6 = x6 - (carry6 << 26);
            var carry7 = x7 >> 25;
            x8 = x8 + carry7;
            x7 = x7 - (carry7 << 25);
            var carry8 = x8 >> 26;
            x9 = x9 + carry8;
            x8 = x8 - (carry8 << 26);
            var carry9 = x9 >> 25;
            x9 = x9 - (carry9 << 25);

            output[0] = (byte)(x0 >> 0);
            output[1] = (byte)(x0 >> 8);
            output[2] = (byte)(x0 >> 16);
            output[3] = (byte)((x0 >> 24) | (x1 << 2));
            output[4] = (byte)(x1 >> 6);
            output[5] = (byte)(x1 >> 14);
            output[6] = (byte)((x1 >> 22) | (x2 << 3));
            output[7] = (byte)(x2 >> 5);
            output[8] = (byte)(x2 >> 13);
            output[9] = (byte)((x2 >> 21) | (x3 << 5));
            output[10] = (byte)(x3 >> 3);
            output[11] = (byte)(x3 >> 11);
            output[12] = (byte)((x3 >> 19) | (x4 << 6));
            output[13] = (byte)(x4 >> 2);
            output[14] = (byte)(x4 >> 10);
            output[15] = (byte)(x4 >> 18);
            output[16] = (byte)(x5 >> 0);
            output[17] = (byte)(x5 >> 8);
            output[18] = (byte)(x5 >> 16);
            output[19] = (byte)((x5 >> 24) | (x6 << 1));
            output[20] = (byte)(x6 >> 7);
            output[21] = (byte)(x6 >> 15);
            output[22] = (byte)((x6 >> 23) | (x7 << 3));
            output[23] = (byte)(x7 >> 5);
            output[24] = (byte)(x7 >> 13);
            output[25] = (byte)((x7 >> 21) | (x8 << 4));
            output[26] = (byte)(x8 >> 4);
            output[27] = (byte)(x8 >> 12);
            output[28] = (byte)((x8 >> 20) | (x9 << 6));
            output[29] = (byte)(x9 >> 2);
            output[30] = (byte)(x9 >> 10);
            output[31] = (byte)(x9 >> 18);
        }

        /// <summary>
        ///     Set the field element to `0`
        /// </summary>
        public static FieldElement Zero()
        {
            return new FieldElement();
        }

        /// <summary>
        ///     Set the field element to `1`
        /// </summary>
        public static FieldElement One()
        {
            var result = Zero();
            result.x0 = 1;
            return result;
        }

        /// <summary>
        ///     Add two field elements
        /// </summary>
        public static void Add(ref FieldElement output, ref FieldElement a, ref FieldElement b)
        {
            output.x0 = a.x0 + b.x0;
            output.x1 = a.x1 + b.x1;
            output.x2 = a.x2 + b.x2;
            output.x3 = a.x3 + b.x3;
            output.x4 = a.x4 + b.x4;
            output.x5 = a.x5 + b.x5;
            output.x6 = a.x6 + b.x6;
            output.x7 = a.x7 + b.x7;
            output.x8 = a.x8 + b.x8;
            output.x9 = a.x9 + b.x9;
        }

        /// <summary>
        ///     Subtract two field elements
        /// </summary>
        public static void Sub(ref FieldElement output, ref FieldElement a, ref FieldElement b)
        {
            output.x0 = a.x0 - b.x0;
            output.x1 = a.x1 - b.x1;
            output.x2 = a.x2 - b.x2;
            output.x3 = a.x3 - b.x3;
            output.x4 = a.x4 - b.x4;
            output.x5 = a.x5 - b.x5;
            output.x6 = a.x6 - b.x6;
            output.x7 = a.x7 - b.x7;
            output.x8 = a.x8 - b.x8;
            output.x9 = a.x9 - b.x9;
        }

        /// <summary>
        ///     Multiply two field elements
        /// </summary>
        public static void Multiply(ref FieldElement output, ref FieldElement a, ref FieldElement b)
        {
            var b1_19 = 19 * b.x1;
            var b2_19 = 19 * b.x2;
            var b3_19 = 19 * b.x3;
            var b4_19 = 19 * b.x4;
            var b5_19 = 19 * b.x5;
            var b6_19 = 19 * b.x6;
            var b7_19 = 19 * b.x7;
            var b8_19 = 19 * b.x8;
            var b9_19 = 19 * b.x9;

            var a1_2 = 2 * a.x1;
            var a3_2 = 2 * a.x3;
            var a5_2 = 2 * a.x5;
            var a7_2 = 2 * a.x7;
            var a9_2 = 2 * a.x9;

            var a0b0 = a.x0 * (long)b.x0;
            var a0b1 = a.x0 * (long)b.x1;
            var a0b2 = a.x0 * (long)b.x2;
            var a0b3 = a.x0 * (long)b.x3;
            var a0b4 = a.x0 * (long)b.x4;
            var a0b5 = a.x0 * (long)b.x5;
            var a0b6 = a.x0 * (long)b.x6;
            var a0b7 = a.x0 * (long)b.x7;
            var a0b8 = a.x0 * (long)b.x8;
            var a0b9 = a.x0 * (long)b.x9;
            var a1b0 = a.x1 * (long)b.x0;
            var a1b1_2 = a1_2 * (long)b.x1;
            var a1b2 = a.x1 * (long)b.x2;
            var a1b3_2 = a1_2 * (long)b.x3;
            var a1b4 = a.x1 * (long)b.x4;
            var a1b5_2 = a1_2 * (long)b.x5;
            var a1b6 = a.x1 * (long)b.x6;
            var a1b7_2 = a1_2 * (long)b.x7;
            var a1b8 = a.x1 * (long)b.x8;
            var a1b9_38 = a1_2 * (long)b9_19;
            var a2b0 = a.x2 * (long)b.x0;
            var a2b1 = a.x2 * (long)b.x1;
            var a2b2 = a.x2 * (long)b.x2;
            var a2b3 = a.x2 * (long)b.x3;
            var a2b4 = a.x2 * (long)b.x4;
            var a2b5 = a.x2 * (long)b.x5;
            var a2b6 = a.x2 * (long)b.x6;
            var a2b7 = a.x2 * (long)b.x7;
            var a2b8_19 = a.x2 * (long)b8_19;
            var a2b9_19 = a.x2 * (long)b9_19;
            var a3b0 = a.x3 * (long)b.x0;
            var a3b1_2 = a3_2 * (long)b.x1;
            var a3b2 = a.x3 * (long)b.x2;
            var a3b3_2 = a3_2 * (long)b.x3;
            var a3b4 = a.x3 * (long)b.x4;
            var a3b5_2 = a3_2 * (long)b.x5;
            var a3b6 = a.x3 * (long)b.x6;
            var a3b7_38 = a3_2 * (long)b7_19;
            var a3b8_19 = a.x3 * (long)b8_19;
            var a3b9_38 = a3_2 * (long)b9_19;
            var a4b0 = a.x4 * (long)b.x0;
            var a4b1 = a.x4 * (long)b.x1;
            var a4b2 = a.x4 * (long)b.x2;
            var a4b3 = a.x4 * (long)b.x3;
            var a4b4 = a.x4 * (long)b.x4;
            var a4b5 = a.x4 * (long)b.x5;
            var a4b6_19 = a.x4 * (long)b6_19;
            var a4b7_19 = a.x4 * (long)b7_19;
            var a4b8_19 = a.x4 * (long)b8_19;
            var a4b9_19 = a.x4 * (long)b9_19;
            var a5b0 = a.x5 * (long)b.x0;
            var a5b1_2 = a5_2 * (long)b.x1;
            var a5b2 = a.x5 * (long)b.x2;
            var a5b3_2 = a5_2 * (long)b.x3;
            var a5b4 = a.x5 * (long)b.x4;
            var a5b5_38 = a5_2 * (long)b5_19;
            var a5b6_19 = a.x5 * (long)b6_19;
            var a5b7_38 = a5_2 * (long)b7_19;
            var a5b8_19 = a.x5 * (long)b8_19;
            var a5b9_38 = a5_2 * (long)b9_19;
            var a6b0 = a.x6 * (long)b.x0;
            var a6b1 = a.x6 * (long)b.x1;
            var a6b2 = a.x6 * (long)b.x2;
            var a6b3 = a.x6 * (long)b.x3;
            var a6b4_19 = a.x6 * (long)b4_19;
            var a6b5_19 = a.x6 * (long)b5_19;
            var a6b6_19 = a.x6 * (long)b6_19;
            var a6b7_19 = a.x6 * (long)b7_19;
            var a6b8_19 = a.x6 * (long)b8_19;
            var a6b9_19 = a.x6 * (long)b9_19;
            var a7b0 = a.x7 * (long)b.x0;
            var a7b1_2 = a7_2 * (long)b.x1;
            var a7b2 = a.x7 * (long)b.x2;
            var a7b3_38 = a7_2 * (long)b3_19;
            var a7b4_19 = a.x7 * (long)b4_19;
            var a7b5_38 = a7_2 * (long)b5_19;
            var a7b6_19 = a.x7 * (long)b6_19;
            var a7b7_38 = a7_2 * (long)b7_19;
            var a7b8_19 = a.x7 * (long)b8_19;
            var a7b9_38 = a7_2 * (long)b9_19;
            var a8b0 = a.x8 * (long)b.x0;
            var a8b1 = a.x8 * (long)b.x1;
            var a8b2_19 = a.x8 * (long)b2_19;
            var a8b3_19 = a.x8 * (long)b3_19;
            var a8b4_19 = a.x8 * (long)b4_19;
            var a8b5_19 = a.x8 * (long)b5_19;
            var a8b6_19 = a.x8 * (long)b6_19;
            var a8b7_19 = a.x8 * (long)b7_19;
            var a8b8_19 = a.x8 * (long)b8_19;
            var a8b9_19 = a.x8 * (long)b9_19;
            var a9b0 = a.x9 * (long)b.x0;
            var a9b1_38 = a9_2 * (long)b1_19;
            var a9b2_19 = a.x9 * (long)b2_19;
            var a9b3_38 = a9_2 * (long)b3_19;
            var a9b4_19 = a.x9 * (long)b4_19;
            var a9b5_38 = a9_2 * (long)b5_19;
            var a9b6_19 = a.x9 * (long)b6_19;
            var a9b7_38 = a9_2 * (long)b7_19;
            var a9b8_19 = a.x9 * (long)b8_19;
            var a9b9_38 = a9_2 * (long)b9_19;

            var h0 = a0b0 + a1b9_38 + a2b8_19 + a3b7_38 + a4b6_19 + a5b5_38 + a6b4_19 + a7b3_38 + a8b2_19 + a9b1_38;
            var h1 = a0b1 + a1b0 + a2b9_19 + a3b8_19 + a4b7_19 + a5b6_19 + a6b5_19 + a7b4_19 + a8b3_19 + a9b2_19;
            var h2 = a0b2 + a1b1_2 + a2b0 + a3b9_38 + a4b8_19 + a5b7_38 + a6b6_19 + a7b5_38 + a8b4_19 + a9b3_38;
            var h3 = a0b3 + a1b2 + a2b1 + a3b0 + a4b9_19 + a5b8_19 + a6b7_19 + a7b6_19 + a8b5_19 + a9b4_19;
            var h4 = a0b4 + a1b3_2 + a2b2 + a3b1_2 + a4b0 + a5b9_38 + a6b8_19 + a7b7_38 + a8b6_19 + a9b5_38;
            var h5 = a0b5 + a1b4 + a2b3 + a3b2 + a4b1 + a5b0 + a6b9_19 + a7b8_19 + a8b7_19 + a9b6_19;
            var h6 = a0b6 + a1b5_2 + a2b4 + a3b3_2 + a4b2 + a5b1_2 + a6b0 + a7b9_38 + a8b8_19 + a9b7_38;
            var h7 = a0b7 + a1b6 + a2b5 + a3b4 + a4b3 + a5b2 + a6b1 + a7b0 + a8b9_19 + a9b8_19;
            var h8 = a0b8 + a1b7_2 + a2b6 + a3b5_2 + a4b4 + a5b3_2 + a6b2 + a7b1_2 + a8b0 + a9b9_38;
            var h9 = a0b9 + a1b8 + a2b7 + a3b6 + a4b5 + a5b4 + a6b3 + a7b2 + a8b1 + a9b0;

            var carry0 = (h0 + (1L << 25)) >> 26;
            h1 += carry0;
            h0 -= carry0 << 26;
            var carry4 = (h4 + (1L << 25)) >> 26;
            h5 += carry4;
            h4 -= carry4 << 26;

            var carry1 = (h1 + (1L << 24)) >> 25;
            h2 += carry1;
            h1 -= carry1 << 25;
            var carry5 = (h5 + (1L << 24)) >> 25;
            h6 += carry5;
            h5 -= carry5 << 25;

            var carry2 = (h2 + (1L << 25)) >> 26;
            h3 += carry2;
            h2 -= carry2 << 26;
            var carry6 = (h6 + (1L << 25)) >> 26;
            h7 += carry6;
            h6 -= carry6 << 26;

            var carry3 = (h3 + (1L << 24)) >> 25;
            h4 += carry3;
            h3 -= carry3 << 25;
            var carry7 = (h7 + (1L << 24)) >> 25;
            h8 += carry7;
            h7 -= carry7 << 25;

            carry4 = (h4 + (1L << 25)) >> 26;
            h5 += carry4;
            h4 -= carry4 << 26;
            var carry8 = (h8 + (1L << 25)) >> 26;
            h9 += carry8;
            h8 -= carry8 << 26;

            var carry9 = (h9 + (1L << 24)) >> 25;
            h0 += carry9 * 19;
            h9 -= carry9 << 25;

            carry0 = (h0 + (1L << 25)) >> 26;
            h1 += carry0;
            h0 -= carry0 << 26;

            output.x0 = (int)h0;
            output.x1 = (int)h1;
            output.x2 = (int)h2;
            output.x3 = (int)h3;
            output.x4 = (int)h4;
            output.x5 = (int)h5;
            output.x6 = (int)h6;
            output.x7 = (int)h7;
            output.x8 = (int)h8;
            output.x9 = (int)h9;
        }

        /// <summary>
        ///     Square a field element
        /// </summary>
        public static void Square(ref FieldElement output, ref FieldElement a)
        {
            var a0_2 = a.x0 * 2;
            var a1_2 = a.x1 * 2;
            var a2_2 = a.x2 * 2;
            var a3_2 = a.x3 * 2;
            var a4_2 = a.x4 * 2;
            var a5_2 = a.x5 * 2;
            var a6_2 = a.x6 * 2;
            var a7_2 = a.x7 * 2;

            var a5_38 = a.x5 * 38;
            var a6_19 = a.x6 * 19;
            var a7_38 = a.x7 * 38;
            var a8_19 = a.x8 * 19;
            var a9_38 = a.x9 * 38;

            var a0a0 = a.x0 * (long)a.x0;
            var a0a1_2 = a0_2 * (long)a.x1;
            var a0a2_2 = a0_2 * (long)a.x2;
            var a0a3_2 = a0_2 * (long)a.x3;
            var a0a4_2 = a0_2 * (long)a.x4;
            var a0a5_2 = a0_2 * (long)a.x5;
            var a0a6_2 = a0_2 * (long)a.x6;
            var a0a7_2 = a0_2 * (long)a.x7;
            var a0a8_2 = a0_2 * (long)a.x8;
            var a0a9_2 = a0_2 * (long)a.x9;
            var a1a1_2 = a1_2 * (long)a.x1;
            var a1a2_2 = a1_2 * (long)a.x2;
            var a1a3_4 = a1_2 * (long)a3_2;
            var a1a4_2 = a1_2 * (long)a.x4;
            var a1a5_4 = a1_2 * (long)a5_2;
            var a1a6_2 = a1_2 * (long)a.x6;
            var a1a7_4 = a1_2 * (long)a7_2;
            var a1a8_2 = a1_2 * (long)a.x8;
            var a1a9_76 = a1_2 * (long)a9_38;
            var a2a2 = a.x2 * (long)a.x2;
            var a2a3_2 = a2_2 * (long)a.x3;
            var a2a4_2 = a2_2 * (long)a.x4;
            var a2a5_2 = a2_2 * (long)a.x5;
            var a2a6_2 = a2_2 * (long)a.x6;
            var a2a7_2 = a2_2 * (long)a.x7;
            var a2a8_38 = a2_2 * (long)a8_19;
            var a2a9_38 = a.x2 * (long)a9_38;
            var a3a3_2 = a3_2 * (long)a.x3;
            var a3a4_2 = a3_2 * (long)a.x4;
            var a3a5_4 = a3_2 * (long)a5_2;
            var a3a6_2 = a3_2 * (long)a.x6;
            var a3a7_76 = a3_2 * (long)a7_38;
            var a3a8_38 = a3_2 * (long)a8_19;
            var a3a9_76 = a3_2 * (long)a9_38;
            var a4a4 = a.x4 * (long)a.x4;
            var a4a5_2 = a4_2 * (long)a.x5;
            var a4a6_38 = a4_2 * (long)a6_19;
            var a4a7_38 = a.x4 * (long)a7_38;
            var a4a8_38 = a4_2 * (long)a8_19;
            var a4a9_38 = a.x4 * (long)a9_38;
            var a5a5_38 = a.x5 * (long)a5_38;
            var a5a6_38 = a5_2 * (long)a6_19;
            var a5a7_76 = a5_2 * (long)a7_38;
            var a5a8_38 = a5_2 * (long)a8_19;
            var a5a9_76 = a5_2 * (long)a9_38;
            var a6a6_19 = a.x6 * (long)a6_19;
            var a6a7_38 = a.x6 * (long)a7_38;
            var a6a8_38 = a6_2 * (long)a8_19;
            var a6a9_38 = a.x6 * (long)a9_38;
            var a7a7_38 = a.x7 * (long)a7_38;
            var a7a8_38 = a7_2 * (long)a8_19;
            var a7a9_76 = a7_2 * (long)a9_38;
            var a8a8_19 = a.x8 * (long)a8_19;
            var a8a9_38 = a.x8 * (long)a9_38;
            var a9a9_38 = a.x9 * (long)a9_38;

            var h0 = a0a0 + a1a9_76 + a2a8_38 + a3a7_76 + a4a6_38 + a5a5_38;
            var h1 = a0a1_2 + a2a9_38 + a3a8_38 + a4a7_38 + a5a6_38;
            var h2 = a0a2_2 + a1a1_2 + a3a9_76 + a4a8_38 + a5a7_76 + a6a6_19;
            var h3 = a0a3_2 + a1a2_2 + a4a9_38 + a5a8_38 + a6a7_38;
            var h4 = a0a4_2 + a1a3_4 + a2a2 + a5a9_76 + a6a8_38 + a7a7_38;
            var h5 = a0a5_2 + a1a4_2 + a2a3_2 + a6a9_38 + a7a8_38;
            var h6 = a0a6_2 + a1a5_4 + a2a4_2 + a3a3_2 + a7a9_76 + a8a8_19;
            var h7 = a0a7_2 + a1a6_2 + a2a5_2 + a3a4_2 + a8a9_38;
            var h8 = a0a8_2 + a1a7_4 + a2a6_2 + a3a5_4 + a4a4 + a9a9_38;
            var h9 = a0a9_2 + a1a8_2 + a2a7_2 + a3a6_2 + a4a5_2;

            var carry0 = (h0 + (1L << 25)) >> 26;
            h1 += carry0;
            h0 -= carry0 << 26;
            var carry4 = (h4 + (1L << 25)) >> 26;
            h5 += carry4;
            h4 -= carry4 << 26;

            var carry1 = (h1 + (1L << 24)) >> 25;
            h2 += carry1;
            h1 -= carry1 << 25;
            var carry5 = (h5 + (1L << 24)) >> 25;
            h6 += carry5;
            h5 -= carry5 << 25;

            var carry2 = (h2 + (1L << 25)) >> 26;
            h3 += carry2;
            h2 -= carry2 << 26;
            var carry6 = (h6 + (1L << 25)) >> 26;
            h7 += carry6;
            h6 -= carry6 << 26;

            var carry3 = (h3 + (1L << 24)) >> 25;
            h4 += carry3;
            h3 -= carry3 << 25;
            var carry7 = (h7 + (1L << 24)) >> 25;
            h8 += carry7;
            h7 -= carry7 << 25;

            carry4 = (h4 + (1L << 25)) >> 26;
            h5 += carry4;
            h4 -= carry4 << 26;
            var carry8 = (h8 + (1L << 25)) >> 26;
            h9 += carry8;
            h8 -= carry8 << 26;

            var carry9 = (h9 + (1L << 24)) >> 25;
            h0 += carry9 * 19;
            h9 -= carry9 << 25;

            carry0 = (h0 + (1L << 25)) >> 26;
            h1 += carry0;
            h0 -= carry0 << 26;

            output.x0 = (int)h0;
            output.x1 = (int)h1;
            output.x2 = (int)h2;
            output.x3 = (int)h3;
            output.x4 = (int)h4;
            output.x5 = (int)h5;
            output.x6 = (int)h6;
            output.x7 = (int)h7;
            output.x8 = (int)h8;
            output.x9 = (int)h9;
        }

        /// <summary>
        ///     Multiplay a field element by 121666
        /// </summary>
        public static void Multiply121666(ref FieldElement output, ref FieldElement a)
        {
            var h0 = a.x0 * 121666L;
            var h1 = a.x1 * 121666L;
            var h2 = a.x2 * 121666L;
            var h3 = a.x3 * 121666L;
            var h4 = a.x4 * 121666L;
            var h5 = a.x5 * 121666L;
            var h6 = a.x6 * 121666L;
            var h7 = a.x7 * 121666L;
            var h8 = a.x8 * 121666L;
            var h9 = a.x9 * 121666L;

            var carry9 = (h9 + (1L << 24)) >> 25;
            h0 += carry9 * 19;
            h9 -= carry9 << 25;
            var carry1 = (h1 + (1L << 24)) >> 25;
            h2 += carry1;
            h1 -= carry1 << 25;
            var carry3 = (h3 + (1L << 24)) >> 25;
            h4 += carry3;
            h3 -= carry3 << 25;
            var carry5 = (h5 + (1L << 24)) >> 25;
            h6 += carry5;
            h5 -= carry5 << 25;
            var carry7 = (h7 + (1L << 24)) >> 25;
            h8 += carry7;
            h7 -= carry7 << 25;

            var carry0 = (h0 + (1L << 25)) >> 26;
            h1 += carry0;
            h0 -= carry0 << 26;
            var carry2 = (h2 + (1L << 25)) >> 26;
            h3 += carry2;
            h2 -= carry2 << 26;
            var carry4 = (h4 + (1L << 25)) >> 26;
            h5 += carry4;
            h4 -= carry4 << 26;
            var carry6 = (h6 + (1L << 25)) >> 26;
            h7 += carry6;
            h6 -= carry6 << 26;
            var carry8 = (h8 + (1L << 25)) >> 26;
            h9 += carry8;
            h8 -= carry8 << 26;

            output.x0 = (int)h0;
            output.x1 = (int)h1;
            output.x2 = (int)h2;
            output.x3 = (int)h3;
            output.x4 = (int)h4;
            output.x5 = (int)h5;
            output.x6 = (int)h6;
            output.x7 = (int)h7;
            output.x8 = (int)h8;
            output.x9 = (int)h9;
        }

        /// <summary>
        ///     Invert a field element
        /// </summary>
        public static void Invert(ref FieldElement output, ref FieldElement a)
        {
            var t0 = new FieldElement();
            Square(ref t0, ref a);

            var t1 = new FieldElement();
            Square(ref t1, ref t0);
            Square(ref t1, ref t1);

            var t2 = new FieldElement();
            Multiply(ref t1, ref a, ref t1);
            Multiply(ref t0, ref t0, ref t1);
            Square(ref t2, ref t0);
            //Square(ref t2, ref t2);

            Multiply(ref t1, ref t1, ref t2);
            Square(ref t2, ref t1);
            for (var ii = 1; ii < 5; ++ii) Square(ref t2, ref t2);

            Multiply(ref t1, ref t2, ref t1);
            Square(ref t2, ref t1);
            for (var ii = 1; ii < 10; ++ii) Square(ref t2, ref t2);

            var t3 = new FieldElement();
            Multiply(ref t2, ref t2, ref t1);
            Square(ref t3, ref t2);
            for (var ii = 1; ii < 20; ++ii) Square(ref t3, ref t3);

            Multiply(ref t2, ref t3, ref t2);
            Square(ref t2, ref t2);
            for (var ii = 1; ii < 10; ++ii) Square(ref t2, ref t2);

            Multiply(ref t1, ref t2, ref t1);
            Square(ref t2, ref t1);
            for (var ii = 1; ii < 50; ++ii) Square(ref t2, ref t2);

            Multiply(ref t2, ref t2, ref t1);
            Square(ref t3, ref t2);
            for (var ii = 1; ii < 100; ++ii) Square(ref t3, ref t3);

            Multiply(ref t2, ref t3, ref t2);
            Square(ref t2, ref t2);
            for (var ii = 1; ii < 50; ++ii) Square(ref t2, ref t2);

            Multiply(ref t1, ref t2, ref t1);
            Square(ref t1, ref t1);
            for (var ii = 1; ii < 5; ++ii) Square(ref t1, ref t1);

            Multiply(ref output, ref t1, ref t0);
        }

        /// <summary>
        ///     Swaps `a` and `b` if `swap` is 1
        /// </summary>
        public static void ConditionalSwap(ref FieldElement a, ref FieldElement b, int swap)
        {
            Debug.Assert(swap == 0 || swap == 1);
            swap = -swap;

            var temp = new FieldElement
            {
                x0 = swap & (a.x0 ^ b.x0),
                x1 = swap & (a.x1 ^ b.x1),
                x2 = swap & (a.x2 ^ b.x2),
                x3 = swap & (a.x3 ^ b.x3),
                x4 = swap & (a.x4 ^ b.x4),
                x5 = swap & (a.x5 ^ b.x5),
                x6 = swap & (a.x6 ^ b.x6),
                x7 = swap & (a.x7 ^ b.x7),
                x8 = swap & (a.x8 ^ b.x8),
                x9 = swap & (a.x9 ^ b.x9)
            };

            a.x0 ^= temp.x0;
            a.x1 ^= temp.x1;
            a.x2 ^= temp.x2;
            a.x3 ^= temp.x3;
            a.x4 ^= temp.x4;
            a.x5 ^= temp.x5;
            a.x6 ^= temp.x6;
            a.x7 ^= temp.x7;
            a.x8 ^= temp.x8;
            a.x9 ^= temp.x9;

            b.x0 ^= temp.x0;
            b.x1 ^= temp.x1;
            b.x2 ^= temp.x2;
            b.x3 ^= temp.x3;
            b.x4 ^= temp.x4;
            b.x5 ^= temp.x5;
            b.x6 ^= temp.x6;
            b.x7 ^= temp.x7;
            b.x8 ^= temp.x8;
            b.x9 ^= temp.x9;
        }
    }
}
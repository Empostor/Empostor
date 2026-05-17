using System;

namespace Next.Hazel;

/// <summary>
///     This is a minimal implementation of `System.Span` in .NET 5.0
/// </summary>
public struct ByteSpan
{
    private readonly byte[] array_;

    /// <summary>
    ///     Create a new span object containing an entire array
    /// </summary>
    public ByteSpan(byte[] array)
    {
        if (array == null)
        {
            array_ = null;
            Offset = 0;
            Length = 0;
        }
        else
        {
            array_ = array;
            Offset = 0;
            Length = array.Length;
        }
    }

    /// <summary>
    ///     Creates a new span object containing a subset of an array
    /// </summary>
    public ByteSpan(byte[] array, int offset, int length)
    {
        if (array == null)
        {
            if (offset != 0) throw new ArgumentException("Invalid offset", nameof(offset));
            if (length != 0) throw new ArgumentException("Invalid length", nameof(offset));

            array_ = null;
            Offset = 0;
            Length = 0;
        }
        else
        {
            if (offset < 0 || offset > array.Length) throw new ArgumentException("Invalid offset", nameof(offset));
            if (length < 0) throw new ArgumentException($"Invalid length: {length}", nameof(length));
            if (offset + length > array.Length)
                throw new ArgumentException(
                    $"Invalid length. Length: {length} Offset: {offset} Array size: {array.Length}", nameof(length));

            array_ = array;
            Offset = offset;
            Length = length;
        }
    }

    /// <summary>
    ///     Returns the underlying array.
    ///     WARNING: This does not return the span, but the entire underlying storage block
    /// </summary>
    public byte[] GetUnderlyingArray()
    {
        return array_;
    }

    /// <summary>
    ///     Returns the offset into the underlying array
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///     Returns the length of the current span
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Gets the span element at the specified index
    /// </summary>
    public byte this[int index]
    {
        get
        {
            if (index < 0 || index >= Length) throw new IndexOutOfRangeException();

            return array_[Offset + index];
        }
        set
        {
            if (index < 0 || index >= Length) throw new IndexOutOfRangeException();

            array_[Offset + index] = value;
        }
    }

    /// <summary>
    ///     Create a new span that is a subset of this span [offset, this.Length-offset)
    /// </summary>
    public ByteSpan Slice(int offset)
    {
        return Slice(offset, Length - offset);
    }

    /// <summary>
    ///     Create a new span that is a subset of this span [offset, length)
    /// </summary>
    public ByteSpan Slice(int offset, int length)
    {
        return new ByteSpan(array_, Offset + offset, length);
    }

    /// <summary>
    ///     Copies the contents of the span to an array
    /// </summary>
    public void CopyTo(byte[] array, int offset)
    {
        CopyTo(new ByteSpan(array, offset, array.Length - offset));
    }

    /// <summary>
    ///     Copies the contents of the span to another span
    /// </summary>
    public void CopyTo(ByteSpan destination)
    {
        if (destination.Length < Length)
            throw new ArgumentException("Destination span is shorter than source", nameof(destination));

        if (Length > 0) Buffer.BlockCopy(array_, Offset, destination.array_, destination.Offset, Length);
    }

    /// <summary>
    ///     Create a new array with the contents of this span
    /// </summary>
    public byte[] ToArray()
    {
        var result = new byte[Length];
        CopyTo(result);
        return result;
    }

    /// <summary>
    ///     Implicit conversion from byte[] -> ByteSpan
    /// </summary>
    public static implicit operator ByteSpan(byte[] array)
    {
        return new ByteSpan(array);
    }

    /// <summary>
    ///     Retuns an empty span object
    /// </summary>
    public static ByteSpan Empty => new(null);
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Next.Hazel.Abstractions;

namespace Next.Hazel;

public class MessageWriter : IMessageWriter, IRecyclable
{
    public static int BufferSize = 64000;
    private static readonly ObjectPoolCustom<MessageWriter> WriterPool = new(() => new MessageWriter(BufferSize));

    private readonly Stack<int> messageStarts = new();

    public MessageWriter(byte[] buffer)
    {
        Buffer = buffer;
        Length = Buffer.Length;
    }

    public MessageWriter(int bufferSize)
    {
        Buffer = new byte[bufferSize];
    }

    public MessageType SendOption { get; private set; }

    public byte[] Buffer { get; }
    public int Length { get; set; }
    public int Position { get; set; }

    public byte[] ToByteArray(bool includeHeader)
    {
        if (includeHeader)
        {
            var output = new byte[Length];
            System.Buffer.BlockCopy(Buffer, 0, output, 0, Length);
            return output;
        }

        switch (SendOption)
        {
            case MessageType.Reliable:
            {
                var output = new byte[Length - 3];
                System.Buffer.BlockCopy(Buffer, 3, output, 0, Length - 3);
                return output;
            }
            case MessageType.Unreliable:
            {
                var output = new byte[Length - 1];
                System.Buffer.BlockCopy(Buffer, 1, output, 0, Length - 1);
                return output;
            }

            default:
                return [];
        }
    }

    public void StartMessage(byte typeFlag)
    {
        messageStarts.Push(Position);
        Position += 2; // Skip for size
        Write(typeFlag);
    }

    public void EndMessage()
    {
        var lastMessageStart = messageStarts.Pop();
        var length = (ushort)(Position - lastMessageStart - 3); // Minus length and type byte
        Buffer[lastMessageStart] = (byte)length;
        Buffer[lastMessageStart + 1] = (byte)(length >> 8);
    }

    public void Clear(MessageType sendOption)
    {
        messageStarts.Clear();
        SendOption = sendOption;
        Buffer[0] = (byte)sendOption;
        switch (sendOption)
        {
            default:
            case MessageType.Unreliable:
                Length = Position = 1;
                break;

            case MessageType.Reliable:
                Length = Position = 3;
                break;
        }
    }

    public void Dispose()
    {
        Recycle();
    }

    public void Recycle()
    {
        Position = Length = 0;
        WriterPool.PutObject(this);
    }

    /// <param name="sendOption">The option specifying how the message should be sent.</param>
    public static MessageWriter Get(MessageType sendOption = MessageType.Unreliable)
    {
        var output = WriterPool.GetObject();
        output.Clear(sendOption);

        return output;
    }

    public bool HasBytes(int expected)
    {
        if (SendOption == MessageType.Unreliable) return Length > 1 + expected;

        return Length > 3 + expected;
    }

    public void CancelMessage()
    {
        Position = messageStarts.Pop();
        Length = Position;
    }

    #region WriteMethods

    public void Write(bool value)
    {
        Buffer[Position++] = (byte)(value ? 1 : 0);
        if (Position > Length) Length = Position;
    }

    public void Write(sbyte value)
    {
        Buffer[Position++] = (byte)value;
        if (Position > Length) Length = Position;
    }

    public void Write(byte value)
    {
        Buffer[Position++] = value;
        if (Position > Length) Length = Position;
    }

    public void Write(short value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        if (Position > Length) Length = Position;
    }

    public void Write(ushort value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        if (Position > Length) Length = Position;
    }

    public void Write(uint value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        Buffer[Position++] = (byte)(value >> 16);
        Buffer[Position++] = (byte)(value >> 24);
        if (Position > Length) Length = Position;
    }

    public void Write(int value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        Buffer[Position++] = (byte)(value >> 16);
        Buffer[Position++] = (byte)(value >> 24);
        if (Position > Length) Length = Position;
    }

    public void Write(ulong value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        Buffer[Position++] = (byte)(value >> 16);
        Buffer[Position++] = (byte)(value >> 24);
        Buffer[Position++] = (byte)(value >> 32);
        Buffer[Position++] = (byte)(value >> 40);
        Buffer[Position++] = (byte)(value >> 48);
        Buffer[Position++] = (byte)(value >> 56);
        if (Position > Length) Length = Position;
    }

    public void Write(long value)
    {
        Buffer[Position++] = (byte)value;
        Buffer[Position++] = (byte)(value >> 8);
        Buffer[Position++] = (byte)(value >> 16);
        Buffer[Position++] = (byte)(value >> 24);
        Buffer[Position++] = (byte)(value >> 32);
        Buffer[Position++] = (byte)(value >> 40);
        Buffer[Position++] = (byte)(value >> 48);
        Buffer[Position++] = (byte)(value >> 56);
        if (Position > Length) Length = Position;
    }

    public unsafe void Write(float value)
    {
        fixed (byte* ptr = &Buffer[Position])
        {
            var valuePtr = (byte*)&value;

            *ptr = *valuePtr;
            *(ptr + 1) = *(valuePtr + 1);
            *(ptr + 2) = *(valuePtr + 2);
            *(ptr + 3) = *(valuePtr + 3);
        }

        Position += 4;
        if (Position > Length) Length = Position;
    }

    public void Write(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WritePacked(bytes.Length);
        Write(bytes);
    }

    public void WriteBytesAndSize(byte[] bytes)
    {
        WritePacked((uint)bytes.Length);
        Write(bytes);
    }

    public void WriteBytesAndSize(byte[] bytes, int length)
    {
        WritePacked((uint)length);
        Write(bytes, length);
    }

    public void WriteBytesAndSize(byte[] bytes, int offset, int length)
    {
        WritePacked((uint)length);
        Write(bytes, offset, length);
    }

    public void Write(ReadOnlyMemory<byte> data)
    {
        Write(data.Span);
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(Buffer.AsSpan(Position, bytes.Length));

        Position += bytes.Length;
        if (Position > Length) Length = Position;
    }

    public void Write(byte[] bytes)
    {
        Array.Copy(bytes, 0, Buffer, Position, bytes.Length);
        Position += bytes.Length;
        if (Position > Length) Length = Position;
    }

    public void Write(byte[] bytes, int offset, int length)
    {
        Array.Copy(bytes, offset, Buffer, Position, length);
        Position += length;
        if (Position > Length) Length = Position;
    }

    public void Write(byte[] bytes, int length)
    {
        Array.Copy(bytes, 0, Buffer, Position, length);
        Position += length;
        if (Position > Length) Length = Position;
    }

    public void WritePacked(int value)
    {
        WritePacked((uint)value);
    }

    public void WritePacked(uint value)
    {
        do
        {
            var b = (byte)(value & 0xFF);
            if (value >= 0x80) b |= 0x80;

            Write(b);
            value >>= 7;
        } while (value > 0);
    }

    public void Write(MessageWriter msg, bool includeHeader)
    {
        var offset = 0;
        if (!includeHeader)
            switch (msg.SendOption)
            {
                case MessageType.Unreliable:
                    offset = 1;
                    break;
                case MessageType.Reliable:
                    offset = 3;
                    break;
            }

        Write(msg.Buffer, offset, msg.Length - offset);
    }

    public void Write(IPAddress value)
    {
        Write(value.GetAddressBytes());
    }

    #endregion
}
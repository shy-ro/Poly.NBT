namespace Poly.NBT.Internal;

internal static class StreamIO
{
    public delegate void SpanWriter<T>(Span<byte> destination, T value);

    public static byte[] ReadExactly(Stream stream, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(length);
        stream.ReadExactly(buffer);
        return buffer;
    }

    public static unsafe void Write<T>(Stream stream, T value, SpanWriter<T> writer) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[sizeof(T)];
        writer(buffer, value);
        stream.Write(buffer);
    }
}

internal static class VarInt
{
    public static uint ReadUInt32(Stream stream)
    {
        ulong value = Read(stream, 5);
        return checked((uint)value);
    }

    public static ulong ReadUInt64(Stream stream) => Read(stream, 10);

    public static void WriteUInt32(Stream stream, uint value) => Write(stream, value);
    public static void WriteUInt64(Stream stream, ulong value) => Write(stream, value);
    public static uint ZigZagEncode(int value) => unchecked((uint)((value << 1) ^ (value >> 31)));
    public static ulong ZigZagEncode(long value) => unchecked((ulong)((value << 1) ^ (value >> 63)));
    public static int ZigZagDecode32(uint value) => (int)(value >> 1) ^ -((int)value & 1);
    public static long ZigZagDecode64(ulong value) => (long)(value >> 1) ^ -((long)value & 1);

    private static ulong Read(Stream stream, int maxBytes)
    {
        ulong result = 0;
        for (int index = 0; index < maxBytes; index++)
        {
            int current = stream.ReadByte();
            if (current < 0)
            {
                throw new EndOfStreamException();
            }

            result |= (ulong)(current & 0x7f) << (index * 7);
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw new FormatException("The VarInt exceeds its maximum encoded length.");
    }

    private static void Write(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}

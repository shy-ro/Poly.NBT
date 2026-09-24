using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Poly.NBT.Internal;

namespace Poly.NBT.Serialization;

internal sealed class PrimitiveConverter<T>(
    NbtTagType tagType,
    Func<Stream, T> reader,
    Action<Stream, T> writer) : NbtConverter<T>
{
    public override NbtTagType TagType => tagType;
    public override T ReadPayload(Stream stream, int depth) => reader(stream);
    public override void WritePayload(Stream stream, T? value, int depth) => writer(stream, value!);
}

internal sealed class StringConverter(NbtStringCodec codec) : NbtConverter<string>
{
    public override NbtTagType TagType => NbtTagType.String;
    public override string ReadPayload(Stream stream, int depth) => codec.Read(stream);
    public override void WritePayload(Stream stream, string? value, int depth) => codec.Write(stream, value ?? throw new InvalidDataException("NBT has no null string value."));
}

internal sealed class ByteArrayConverter(NbtLengthCodec lengths, bool optimize) : NbtConverter<byte[]>
{
    public override NbtTagType TagType => optimize ? NbtTagType.ByteArray : NbtTagType.List;
    public override byte[] ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        if (actualType == NbtTagType.List)
        {
            if (stream.ReadByte() != (byte)NbtTagType.Byte) throw new InvalidDataException("Expected TAG_Byte list elements.");
            int count = lengths.ReadCollectionLength(stream);
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++) result[i] = NbtSerializer.ReadByte(stream);
            return result;
        }
        NbtSerializer.EnsureTagType(NbtTagType.ByteArray, actualType);
        return ReadPayload(stream, depth);
    }
    public override byte[] ReadPayload(Stream stream, int depth) => StreamIO.ReadExactly(stream, lengths.ReadCollectionLength(stream));
    public override void WritePayload(Stream stream, byte[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!optimize) stream.WriteByte((byte)NbtTagType.Byte);
        lengths.WriteCollectionLength(stream, value.Length);
        stream.Write(value);
    }
}

internal sealed class SByteArrayConverter(NbtLengthCodec lengths, bool optimize) : NbtConverter<sbyte[]>
{
    public override NbtTagType TagType => optimize ? NbtTagType.ByteArray : NbtTagType.List;
    public override sbyte[] ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        if (actualType == NbtTagType.List)
        {
            if (stream.ReadByte() != (byte)NbtTagType.Byte) throw new InvalidDataException("Expected TAG_Byte list elements.");
            int count = lengths.ReadCollectionLength(stream);
            sbyte[] result = new sbyte[count];
            for (int i = 0; i < count; i++) result[i] = unchecked((sbyte)NbtSerializer.ReadByte(stream));
            return result;
        }
        NbtSerializer.EnsureTagType(NbtTagType.ByteArray, actualType);
        return ReadPayload(stream, depth);
    }
    public override sbyte[] ReadPayload(Stream stream, int depth)
    {
        sbyte[] result = GC.AllocateUninitializedArray<sbyte>(lengths.ReadCollectionLength(stream));
        stream.ReadExactly(MemoryMarshal.AsBytes(result.AsSpan()));
        return result;
    }

    public override void WritePayload(Stream stream, sbyte[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!optimize) stream.WriteByte((byte)NbtTagType.Byte);
        lengths.WriteCollectionLength(stream, value.Length);
        stream.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.AsSpan()));
    }
}

internal sealed class IntArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric, bool optimize) : NbtConverter<int[]>
{
    public override NbtTagType TagType => optimize ? NbtTagType.IntArray : NbtTagType.List;
    public override int[] ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        if (actualType == NbtTagType.List)
        {
            if (stream.ReadByte() != (byte)NbtTagType.Int) throw new InvalidDataException("Expected TAG_Int list elements.");
            int count = lengths.ReadCollectionLength(stream);
            return FixedArrayIO.ReadInt32(stream, count, numeric);
        }
        NbtSerializer.EnsureTagType(NbtTagType.IntArray, actualType);
        return ReadPayload(stream, depth);
    }
    public override int[] ReadPayload(Stream stream, int depth)
    {
        return FixedArrayIO.ReadInt32(stream, lengths.ReadCollectionLength(stream), numeric);
    }

    public override void WritePayload(Stream stream, int[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!optimize) stream.WriteByte((byte)NbtTagType.Int);
        lengths.WriteCollectionLength(stream, value.Length);
        FixedArrayIO.WriteInt32(stream, value, numeric);
    }
}

internal sealed class LongArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric, bool supported, bool optimize) : NbtConverter<long[]>
{
    private readonly bool useArray = supported && optimize;
    public override NbtTagType TagType => useArray ? NbtTagType.LongArray : NbtTagType.List;

    public override long[] ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        if (actualType == NbtTagType.List)
        {
            NbtTagType elementType = ReadTag(stream);
            if (elementType != NbtTagType.Long) throw new InvalidDataException("Expected TAG_Long list elements.");
            int count = lengths.ReadCollectionLength(stream);
            return FixedArrayIO.ReadInt64(stream, count, numeric);
        }
        NbtSerializer.EnsureTagType(NbtTagType.LongArray, actualType);
        return ReadPayload(stream, depth);
    }

    public override long[] ReadPayload(Stream stream, int depth)
    {
        EnsureSupported();
        return FixedArrayIO.ReadInt64(stream, lengths.ReadCollectionLength(stream), numeric);
    }

    public override void WritePayload(Stream stream, long[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!useArray)
        {
            stream.WriteByte((byte)NbtTagType.Long);
            lengths.WriteCollectionLength(stream, value.Length);
            FixedArrayIO.WriteInt64(stream, value, numeric);
            return;
        }
        lengths.WriteCollectionLength(stream, value.Length);
        FixedArrayIO.WriteInt64(stream, value, numeric);
    }

    private void EnsureSupported()
    {
        if (!supported) throw new NotSupportedException("The configured NBT dialect does not support TAG_Long_Array.");
    }

    private static NbtTagType ReadTag(Stream stream)
    {
        int value = stream.ReadByte();
        if (value < 0) throw new EndOfStreamException();
        return (NbtTagType)value;
    }
}

internal static class FixedArrayIO
{
    public static int[] ReadInt32(Stream stream, int count, NbtNumericCodec numeric)
    {
        int[] result = GC.AllocateUninitializedArray<int>(count);
        if (numeric is VarIntNumericCodec)
        {
            for (int i = 0; i < count; i++) result[i] = numeric.ReadInt32(stream);
            return result;
        }
        stream.ReadExactly(MemoryMarshal.AsBytes(result.AsSpan()));
        if (BitConverter.IsLittleEndian != (numeric is LittleEndianNumericCodec))
            for (int i = 0; i < result.Length; i++) result[i] = BinaryPrimitives.ReverseEndianness(result[i]);
        return result;
    }

    public static long[] ReadInt64(Stream stream, int count, NbtNumericCodec numeric)
    {
        long[] result = GC.AllocateUninitializedArray<long>(count);
        if (numeric is VarIntNumericCodec)
        {
            for (int i = 0; i < count; i++) result[i] = numeric.ReadInt64(stream);
            return result;
        }
        stream.ReadExactly(MemoryMarshal.AsBytes(result.AsSpan()));
        if (BitConverter.IsLittleEndian != (numeric is LittleEndianNumericCodec))
            for (int i = 0; i < result.Length; i++) result[i] = BinaryPrimitives.ReverseEndianness(result[i]);
        return result;
    }

    public static void WriteInt32(Stream stream, ReadOnlySpan<int> values, NbtNumericCodec numeric)
    {
        if (numeric is VarIntNumericCodec)
        {
            foreach (int value in values) numeric.WriteInt32(stream, value);
            return;
        }
        WriteFixed(stream, values, numeric is LittleEndianNumericCodec, BinaryPrimitives.ReverseEndianness);
    }

    public static void WriteInt64(Stream stream, ReadOnlySpan<long> values, NbtNumericCodec numeric)
    {
        if (numeric is VarIntNumericCodec)
        {
            foreach (long value in values) numeric.WriteInt64(stream, value);
            return;
        }
        WriteFixed(stream, values, numeric is LittleEndianNumericCodec, BinaryPrimitives.ReverseEndianness);
    }

    private static void WriteFixed<T>(Stream stream, ReadOnlySpan<T> values, bool littleEndian, Func<T, T> reverse) where T : unmanaged
    {
        if (BitConverter.IsLittleEndian == littleEndian)
        {
            stream.Write(MemoryMarshal.AsBytes(values));
            return;
        }

        T[] rented = ArrayPool<T>.Shared.Rent(values.Length);
        try
        {
            Span<T> converted = rented.AsSpan(0, values.Length);
            for (int i = 0; i < values.Length; i++) converted[i] = reverse(values[i]);
            stream.Write(MemoryMarshal.AsBytes(converted));
        }
        finally
        {
            ArrayPool<T>.Shared.Return(rented);
        }
    }
}

internal sealed class FloatArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric) : NbtConverter<float[]>
{
    public override NbtTagType TagType => NbtTagType.List;
    public override float[] ReadPayload(Stream stream, int depth)
    {
        if (NbtSerializer.ReadByte(stream) != (byte)NbtTagType.Float) throw new InvalidDataException("Expected TAG_Float list elements.");
        float[] result = GC.AllocateUninitializedArray<float>(lengths.ReadCollectionLength(stream));
        stream.ReadExactly(MemoryMarshal.AsBytes(result.AsSpan()));
        if (BitConverter.IsLittleEndian != (numeric is not BigEndianNumericCodec))
            for (int i = 0; i < result.Length; i++) result[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(BitConverter.SingleToInt32Bits(result[i])));
        return result;
    }

    public override void WritePayload(Stream stream, float[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        stream.WriteByte((byte)NbtTagType.Float);
        lengths.WriteCollectionLength(stream, value.Length);
        if (BitConverter.IsLittleEndian == (numeric is not BigEndianNumericCodec))
        {
            stream.Write(MemoryMarshal.AsBytes(value.AsSpan()));
            return;
        }
        int[] rented = ArrayPool<int>.Shared.Rent(value.Length);
        try
        {
            Span<int> bits = rented.AsSpan(0, value.Length);
            for (int i = 0; i < value.Length; i++) bits[i] = BinaryPrimitives.ReverseEndianness(BitConverter.SingleToInt32Bits(value[i]));
            stream.Write(MemoryMarshal.AsBytes(bits));
        }
        finally { ArrayPool<int>.Shared.Return(rented); }
    }
}

internal sealed class DoubleArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric) : NbtConverter<double[]>
{
    public override NbtTagType TagType => NbtTagType.List;
    public override double[] ReadPayload(Stream stream, int depth)
    {
        if (NbtSerializer.ReadByte(stream) != (byte)NbtTagType.Double) throw new InvalidDataException("Expected TAG_Double list elements.");
        double[] result = GC.AllocateUninitializedArray<double>(lengths.ReadCollectionLength(stream));
        stream.ReadExactly(MemoryMarshal.AsBytes(result.AsSpan()));
        if (BitConverter.IsLittleEndian != (numeric is not BigEndianNumericCodec))
            for (int i = 0; i < result.Length; i++) result[i] = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(result[i])));
        return result;
    }

    public override void WritePayload(Stream stream, double[]? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        stream.WriteByte((byte)NbtTagType.Double);
        lengths.WriteCollectionLength(stream, value.Length);
        if (BitConverter.IsLittleEndian == (numeric is not BigEndianNumericCodec))
        {
            stream.Write(MemoryMarshal.AsBytes(value.AsSpan()));
            return;
        }
        long[] rented = ArrayPool<long>.Shared.Rent(value.Length);
        try
        {
            Span<long> bits = rented.AsSpan(0, value.Length);
            for (int i = 0; i < value.Length; i++) bits[i] = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value[i]));
            stream.Write(MemoryMarshal.AsBytes(bits));
        }
        finally { ArrayPool<long>.Shared.Return(rented); }
    }
}

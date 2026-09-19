using System.Buffers.Binary;
using Poly.NBT.Internal;

namespace Poly.NBT.Serialization;

internal sealed class PrimitiveConverter<T>(
    NbtTagType tagType,
    Func<Stream, T> reader,
    Action<Stream, T> writer) : NbtConverter<T>
{
    public override NbtTagType TagType => tagType;
    public override T ReadPayload(Stream stream) => reader(stream);
    public override void WritePayload(Stream stream, T? value) => writer(stream, value!);
}

internal sealed class StringConverter(NbtStringCodec codec) : NbtConverter<string>
{
    public override NbtTagType TagType => NbtTagType.String;
    public override string ReadPayload(Stream stream) => codec.Read(stream);
    public override void WritePayload(Stream stream, string? value) => codec.Write(stream, value ?? throw new InvalidDataException("NBT has no null string value."));
}

internal sealed class ByteArrayConverter(NbtLengthCodec lengths, bool optimize) : NbtConverter<byte[]>
{
    public override NbtTagType TagType => optimize ? NbtTagType.ByteArray : NbtTagType.List;
    public override byte[] ReadPayload(Stream stream, NbtTagType actualType)
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
        return ReadPayload(stream);
    }
    public override byte[] ReadPayload(Stream stream) => StreamIO.ReadExactly(stream, lengths.ReadCollectionLength(stream));
    public override void WritePayload(Stream stream, byte[]? value)
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
    public override sbyte[] ReadPayload(Stream stream, NbtTagType actualType)
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
        return ReadPayload(stream);
    }
    public override sbyte[] ReadPayload(Stream stream)
    {
        byte[] bytes = StreamIO.ReadExactly(stream, lengths.ReadCollectionLength(stream));
        sbyte[] result = new sbyte[bytes.Length];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    public override void WritePayload(Stream stream, sbyte[]? value)
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
    public override int[] ReadPayload(Stream stream, NbtTagType actualType)
    {
        if (actualType == NbtTagType.List)
        {
            if (stream.ReadByte() != (byte)NbtTagType.Int) throw new InvalidDataException("Expected TAG_Int list elements.");
            int count = lengths.ReadCollectionLength(stream);
            int[] result = new int[count];
            for (int i = 0; i < count; i++) result[i] = numeric.ReadInt32(stream);
            return result;
        }
        NbtSerializer.EnsureTagType(NbtTagType.IntArray, actualType);
        return ReadPayload(stream);
    }
    public override int[] ReadPayload(Stream stream)
    {
        int[] result = new int[lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = numeric.ReadInt32(stream);
        return result;
    }

    public override void WritePayload(Stream stream, int[]? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!optimize) stream.WriteByte((byte)NbtTagType.Int);
        lengths.WriteCollectionLength(stream, value.Length);
        foreach (int item in value) numeric.WriteInt32(stream, item);
    }
}

internal sealed class LongArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric, bool supported, bool optimize) : NbtConverter<long[]>
{
    private readonly bool useArray = supported && optimize;
    public override NbtTagType TagType => useArray ? NbtTagType.LongArray : NbtTagType.List;

    public override long[] ReadPayload(Stream stream, NbtTagType actualType)
    {
        if (actualType == NbtTagType.List)
        {
            NbtTagType elementType = ReadTag(stream);
            if (elementType != NbtTagType.Long) throw new InvalidDataException("Expected TAG_Long list elements.");
            int count = lengths.ReadCollectionLength(stream);
            long[] values = new long[count];
            for (int index = 0; index < count; index++) values[index] = numeric.ReadInt64(stream);
            return values;
        }
        NbtSerializer.EnsureTagType(NbtTagType.LongArray, actualType);
        return ReadPayload(stream);
    }

    public override long[] ReadPayload(Stream stream)
    {
        EnsureSupported();
        long[] result = new long[lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = numeric.ReadInt64(stream);
        return result;
    }

    public override void WritePayload(Stream stream, long[]? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        if (!useArray)
        {
            stream.WriteByte((byte)NbtTagType.Long);
            lengths.WriteCollectionLength(stream, value.Length);
            foreach (long item in value) numeric.WriteInt64(stream, item);
            return;
        }
        lengths.WriteCollectionLength(stream, value.Length);
        foreach (long item in value) numeric.WriteInt64(stream, item);
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

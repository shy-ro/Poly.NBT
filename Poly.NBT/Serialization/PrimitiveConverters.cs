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

internal sealed class ByteArrayConverter(NbtLengthCodec lengths) : NbtConverter<byte[]>
{
    public override NbtTagType TagType => NbtTagType.ByteArray;
    public override byte[] ReadPayload(Stream stream) => StreamIO.ReadExactly(stream, lengths.ReadCollectionLength(stream));
    public override void WritePayload(Stream stream, byte[]? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        lengths.WriteCollectionLength(stream, value.Length);
        stream.Write(value);
    }
}

internal sealed class SByteArrayConverter(NbtLengthCodec lengths) : NbtConverter<sbyte[]>
{
    public override NbtTagType TagType => NbtTagType.ByteArray;
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
        lengths.WriteCollectionLength(stream, value.Length);
        stream.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.AsSpan()));
    }
}

internal sealed class IntArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric) : NbtConverter<int[]>
{
    public override NbtTagType TagType => NbtTagType.IntArray;
    public override int[] ReadPayload(Stream stream)
    {
        int[] result = new int[lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = numeric.ReadInt32(stream);
        return result;
    }

    public override void WritePayload(Stream stream, int[]? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        lengths.WriteCollectionLength(stream, value.Length);
        foreach (int item in value) numeric.WriteInt32(stream, item);
    }
}

internal sealed class LongArrayConverter(NbtLengthCodec lengths, NbtNumericCodec numeric, bool supported) : NbtConverter<long[]>
{
    public override NbtTagType TagType => NbtTagType.LongArray;

    public override long[] ReadPayload(Stream stream)
    {
        EnsureSupported();
        long[] result = new long[lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = numeric.ReadInt64(stream);
        return result;
    }

    public override void WritePayload(Stream stream, long[]? value)
    {
        EnsureSupported();
        if (value is null) throw new InvalidDataException("NBT has no null array value.");
        lengths.WriteCollectionLength(stream, value.Length);
        foreach (long item in value) numeric.WriteInt64(stream, item);
    }

    private void EnsureSupported()
    {
        if (!supported) throw new NotSupportedException("The configured NBT dialect does not support TAG_Long_Array.");
    }
}

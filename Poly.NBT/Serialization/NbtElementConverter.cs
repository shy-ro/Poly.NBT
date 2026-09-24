using Poly.NBT.Dom;

namespace Poly.NBT.Serialization;

internal sealed class NbtElementConverter(NbtSerializer serializer) : NbtConverter<Dom.NbtElement>
{
    public override NbtTagType TagType => NbtTagType.End;

    public override NbtTagType GetTagType(Dom.NbtElement? value) => value switch
    {
        NbtByte => NbtTagType.Byte,
        NbtShort => NbtTagType.Short,
        NbtInt => NbtTagType.Int,
        NbtLong => NbtTagType.Long,
        NbtFloat => NbtTagType.Float,
        NbtDouble => NbtTagType.Double,
        NbtByteArray => NbtTagType.ByteArray,
        NbtString => NbtTagType.String,
        NbtList => NbtTagType.List,
        NbtCompound => NbtTagType.Compound,
        NbtIntArray => NbtTagType.IntArray,
        NbtLongArray => NbtTagType.LongArray,
        null => throw new InvalidDataException("NBT DOM values cannot be null."),
        _ => throw new NotSupportedException($"Unknown NBT DOM type {value.GetType()}.")
    };

    public override Dom.NbtElement ReadPayload(Stream stream, int depth) => throw new InvalidOperationException("A dynamic NBT tag type is required.");

    public override Dom.NbtElement ReadPayload(Stream stream, NbtTagType actualType, int depth) => actualType switch
    {
        NbtTagType.Byte => new NbtByte(unchecked((sbyte)NbtSerializer.ReadByte(stream))),
        NbtTagType.Short => new NbtShort(serializer.Numeric.ReadInt16(stream)),
        NbtTagType.Int => new NbtInt(serializer.Numeric.ReadInt32(stream)),
        NbtTagType.Long => new NbtLong(serializer.Numeric.ReadInt64(stream)),
        NbtTagType.Float => new NbtFloat(serializer.Numeric.ReadSingle(stream)),
        NbtTagType.Double => new NbtDouble(serializer.Numeric.ReadDouble(stream)),
        NbtTagType.ByteArray => new NbtByteArray(Poly.NBT.Internal.StreamIO.ReadExactly(stream, serializer.Lengths.ReadCollectionLength(stream))),
        NbtTagType.String => new NbtString(serializer.Strings.Read(stream)),
        NbtTagType.List => ReadList(stream, depth),
        NbtTagType.Compound => ReadCompound(stream, depth),
        NbtTagType.IntArray => new NbtIntArray(ReadIntArray(stream, depth)),
        NbtTagType.LongArray => new NbtLongArray(ReadLongArray(stream, depth)),
        _ => throw new InvalidDataException($"Tag type {actualType} has no DOM payload.")
    };

    public override void WritePayload(Stream stream, Dom.NbtElement? value, int depth)
    {
        switch (value)
        {
            case NbtByte item: stream.WriteByte(unchecked((byte)item.Value)); break;
            case NbtShort item: serializer.Numeric.WriteInt16(stream, item.Value); break;
            case NbtInt item: serializer.Numeric.WriteInt32(stream, item.Value); break;
            case NbtLong item: serializer.Numeric.WriteInt64(stream, item.Value); break;
            case NbtFloat item: serializer.Numeric.WriteSingle(stream, item.Value); break;
            case NbtDouble item: serializer.Numeric.WriteDouble(stream, item.Value); break;
            case NbtByteArray item:
                serializer.Lengths.WriteCollectionLength(stream, item.Value.Length);
                stream.Write(item.Value);
                break;
            case NbtString item: serializer.Strings.Write(stream, item.Value); break;
            case NbtList item: WriteList(stream, item, depth); break;
            case NbtCompound item: WriteCompound(stream, item, depth); break;
            case NbtIntArray item:
                serializer.Lengths.WriteCollectionLength(stream, item.Value.Length);
                foreach (int element in item.Value) serializer.Numeric.WriteInt32(stream, element);
                break;
            case NbtLongArray item:
                EnsureLongArray();
                serializer.Lengths.WriteCollectionLength(stream, item.Value.Length);
                foreach (long element in item.Value) serializer.Numeric.WriteInt64(stream, element);
                break;
            case null: throw new InvalidDataException("NBT DOM values cannot be null.");
            default: throw new NotSupportedException($"Unknown NBT DOM type {value.GetType()}.");
        }
    }

    private NbtList ReadList(Stream stream, int depth)
    {
        NbtTagType elementType = serializer.ReadTagType(stream);
        int count = serializer.Lengths.ReadCollectionLength(stream);
        if (count > 0 && elementType == NbtTagType.End) throw new InvalidDataException("A non-empty NBT list cannot use TAG_End elements.");
        Dom.NbtElement[] values = new Dom.NbtElement[count];
        for (int index = 0; index < count; index++) values[index] = ReadPayload(stream, elementType, serializer.Descend(depth));
        return new NbtList(values);
    }

    private void WriteList(Stream stream, NbtList list, int depth)
    {
        NbtTagType type = list.Count == 0 ? NbtTagType.End : GetTagType(list[0]);
        stream.WriteByte((byte)type);
        serializer.Lengths.WriteCollectionLength(stream, list.Count);
        foreach (Dom.NbtElement item in list)
        {
            NbtSerializer.EnsureTagType(type, GetTagType(item));
            WritePayload(stream, item, serializer.Descend(depth));
        }
    }

    private NbtCompound ReadCompound(Stream stream, int depth)
    {
        Dictionary<string, Dom.NbtElement> result = new(StringComparer.Ordinal);
        while (serializer.ReadTagType(stream) is { } type && type != NbtTagType.End)
        {
            string name = serializer.Strings.Read(stream);
            if (!result.TryAdd(name, ReadPayload(stream, type, serializer.Descend(depth)))) throw new InvalidDataException($"Duplicate NBT key '{name}'.");
        }
        return new NbtCompound(result);
    }

    private void WriteCompound(Stream stream, NbtCompound compound, int depth)
    {
        foreach ((string name, Dom.NbtElement value) in compound)
        {
            stream.WriteByte((byte)GetTagType(value));
            serializer.Strings.Write(stream, name);
            WritePayload(stream, value, serializer.Descend(depth));
        }
        stream.WriteByte((byte)NbtTagType.End);
    }

    private int[] ReadIntArray(Stream stream, int depth)
    {
        int[] result = new int[serializer.Lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = serializer.Numeric.ReadInt32(stream);
        return result;
    }

    private long[] ReadLongArray(Stream stream, int depth)
    {
        EnsureLongArray();
        long[] result = new long[serializer.Lengths.ReadCollectionLength(stream)];
        for (int index = 0; index < result.Length; index++) result[index] = serializer.Numeric.ReadInt64(stream);
        return result;
    }

    private void EnsureLongArray()
    {
        if (!serializer.Options.SupportsLongArray) throw new NotSupportedException("The configured NBT dialect does not support TAG_Long_Array.");
    }
}

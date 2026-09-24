using Poly.NBT.Dom;
using PolyType;
using System.Collections.Concurrent;

namespace Poly.NBT.Serialization;

internal sealed class RuntimeObjectConverter(NbtSerializer serializer, ITypeShapeProvider provider) : NbtConverter<object>
{
    private readonly NbtElementConverter _domConverter = new(serializer);
    private readonly ConcurrentDictionary<Type, NbtConverter> _localCache = new();

    public override NbtTagType TagType => NbtTagType.End;

    public override NbtTagType GetTagType(object? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null value.");
        return value is Dom.NbtElement element
            ? _domConverter.GetTagType(element)
            : Resolve(value).GetTagTypeObject(value);
    }

    public override object? ReadPayload(Stream stream, int depth) => throw new InvalidOperationException("A dynamic NBT tag type is required.");

    public override object? ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        Dom.NbtElement element = _domConverter.ReadPayload(stream, actualType, depth);
        return element switch
        {
            NbtByte item => item.Value,
            NbtShort item => item.Value,
            NbtInt item => item.Value,
            NbtLong item => item.Value,
            NbtFloat item => item.Value,
            NbtDouble item => item.Value,
            NbtString item => item.Value,
            NbtByteArray item => item.Value,
            NbtIntArray item => item.Value,
            NbtLongArray item => item.Value,
            _ => element,
        };
    }

    public override void WritePayload(Stream stream, object? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null value.");
        if (value is Dom.NbtElement element)
        {
            _domConverter.WritePayload(stream, element, depth);
            return;
        }

        Resolve(value).WritePayloadObject(stream, value, depth);
    }

    private NbtConverter Resolve(object value)
    {
        if (value.GetType() == typeof(object)) throw new NotSupportedException("A System.Object instance has no NBT representation.");
        return _localCache.GetOrAdd(value.GetType(), type => serializer.GetConverter(type, provider));
    }
}

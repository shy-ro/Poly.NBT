using Poly.NBT.Dom;
using PolyType;

namespace Poly.NBT.Serialization;

internal sealed class RuntimeObjectConverter(NbtSerializer serializer, ITypeShapeProvider provider) : NbtConverter<object>
{
    private readonly NbtElementConverter _domConverter = new(serializer);

    public override NbtTagType TagType => NbtTagType.End;

    public override NbtTagType GetTagType(object? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null value.");
        return value is Dom.NbtElement element
            ? _domConverter.GetTagType(element)
            : Resolve(value).GetTagTypeObject(value);
    }

    public override object? ReadPayload(Stream stream) => throw new InvalidOperationException("A dynamic NBT tag type is required.");

    public override object? ReadPayload(Stream stream, NbtTagType actualType)
    {
        Dom.NbtElement element = _domConverter.ReadPayload(stream, actualType);
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

    public override void WritePayload(Stream stream, object? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null value.");
        if (value is Dom.NbtElement element)
        {
            _domConverter.WritePayload(stream, element);
            return;
        }

        Resolve(value).WritePayloadObject(stream, value);
    }

    private NbtConverter Resolve(object value)
    {
        if (value.GetType() == typeof(object)) throw new NotSupportedException("A System.Object instance has no NBT representation.");
        return serializer.GetConverter(value.GetType(), provider);
    }
}

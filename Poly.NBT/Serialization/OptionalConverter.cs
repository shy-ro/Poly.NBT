using PolyType;
using PolyType.Abstractions;

namespace Poly.NBT.Serialization;

internal sealed class NbtOptionalConverter<TOptional, TElement>(
    NbtConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TElement, TOptional> createSome) : NbtConverter<TOptional>
{
    public override NbtTagType TagType => elementConverter.TagType;
    public override bool ShouldWrite(TOptional? value) => deconstructor(value, out _);
    public override NbtTagType GetTagType(TOptional? value) => deconstructor(value, out TElement? element)
        ? elementConverter.GetTagType(element)
        : NbtTagType.End;
    public override TOptional ReadPayload(Stream stream, NbtTagType actualType, int depth) => createSome(elementConverter.ReadPayload(stream, actualType, depth)!);
    public override TOptional ReadPayload(Stream stream, int depth) => createSome(elementConverter.ReadPayload(stream, depth)!);
    public override void WritePayload(Stream stream, TOptional? value, int depth)
    {
        if (!deconstructor(value, out TElement? element)) throw new InvalidDataException("An absent optional has no NBT payload.");
        elementConverter.WritePayload(stream, element, depth);
    }
}

internal sealed class NbtSurrogateConverter<T, TSurrogate>(IMarshaler<T, TSurrogate> marshaler, NbtConverter<TSurrogate> converter) : NbtConverter<T>
{
    public override NbtTagType TagType => converter.TagType;
    public override bool ShouldWrite(T? value) => converter.ShouldWrite(marshaler.Marshal(value));
    public override NbtTagType GetTagType(T? value) => converter.GetTagType(marshaler.Marshal(value));
    public override T? ReadPayload(Stream stream, NbtTagType actualType, int depth) => marshaler.Unmarshal(converter.ReadPayload(stream, actualType, depth));
    public override T? ReadPayload(Stream stream, int depth) => marshaler.Unmarshal(converter.ReadPayload(stream, depth));
    public override void WritePayload(Stream stream, T? value, int depth) => converter.WritePayload(stream, marshaler.Marshal(value), depth);
}

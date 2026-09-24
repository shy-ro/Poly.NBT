namespace Poly.NBT.Serialization;

internal abstract class NbtConverter
{
    public abstract Type Type { get; }
    public abstract NbtTagType TagType { get; }
    public abstract NbtTagType GetTagTypeObject(object? value);
    public abstract object? ReadPayloadObject(Stream stream, NbtTagType actualType, int depth);
    public abstract void WritePayloadObject(Stream stream, object? value, int depth);
}

/// <summary>Converts one CLR type to and from an NBT payload.</summary>
/// <remarks>
/// The <c>depth</c> parameter of every read and write method is the nesting level of the value being handled,
/// counting the root tag as level one. It travels as a parameter rather than a field because converters are
/// cached in a <c>MultiProviderTypeCache</c> and shared across threads: a depth field would be visible to
/// concurrent serializations. Every container passes <c>serializer.Descend(depth)</c> down, which both
/// increments the level and enforces the configured limit.
/// </remarks>
internal abstract class NbtConverter<T> : NbtConverter
{
    public sealed override Type Type => typeof(T);
    public sealed override NbtTagType GetTagTypeObject(object? value) => GetTagType((T?)value);
    public sealed override object? ReadPayloadObject(Stream stream, NbtTagType actualType, int depth) => ReadPayload(stream, actualType, depth);
    public sealed override void WritePayloadObject(Stream stream, object? value, int depth) => WritePayload(stream, (T?)value, depth);
    public virtual bool ShouldWrite(T? value) => value is not null;
    public virtual NbtTagType GetTagType(T? value) => TagType;
    public virtual T? ReadPayload(Stream stream, NbtTagType actualType, int depth)
    {
        NbtSerializer.EnsureTagType(TagType, actualType);
        return ReadPayload(stream, depth);
    }
    public abstract T? ReadPayload(Stream stream, int depth);
    public abstract void WritePayload(Stream stream, T? value, int depth);
}

internal sealed class DelayedNbtConverter<T>(PolyType.Utilities.DelayedValue<NbtConverter<T>> value) : NbtConverter<T>
{
    public override NbtTagType TagType => value.Result.TagType;
    public override bool ShouldWrite(T? item) => value.Result.ShouldWrite(item);
    public override NbtTagType GetTagType(T? item) => value.Result.GetTagType(item);
    public override T? ReadPayload(Stream stream, NbtTagType actualType, int depth) => value.Result.ReadPayload(stream, actualType, depth);
    public override T? ReadPayload(Stream stream, int depth) => value.Result.ReadPayload(stream, depth);
    public override void WritePayload(Stream stream, T? item, int depth) => value.Result.WritePayload(stream, item, depth);
}

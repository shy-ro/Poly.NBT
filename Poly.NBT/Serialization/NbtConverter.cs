namespace Poly.NBT.Serialization;

internal abstract class NbtConverter
{
    public abstract Type Type { get; }
    public abstract NbtTagType TagType { get; }
    public abstract NbtTagType GetTagTypeObject(object? value);
    public abstract object? ReadPayloadObject(Stream stream, NbtTagType actualType);
    public abstract void WritePayloadObject(Stream stream, object? value);
}

internal abstract class NbtConverter<T> : NbtConverter
{
    public sealed override Type Type => typeof(T);
    public sealed override NbtTagType GetTagTypeObject(object? value) => GetTagType((T?)value);
    public sealed override object? ReadPayloadObject(Stream stream, NbtTagType actualType) => ReadPayload(stream, actualType);
    public sealed override void WritePayloadObject(Stream stream, object? value) => WritePayload(stream, (T?)value);
    public virtual bool ShouldWrite(T? value) => value is not null;
    public virtual NbtTagType GetTagType(T? value) => TagType;
    public virtual T? ReadPayload(Stream stream, NbtTagType actualType)
    {
        NbtSerializer.EnsureTagType(TagType, actualType);
        return ReadPayload(stream);
    }
    public abstract T? ReadPayload(Stream stream);
    public abstract void WritePayload(Stream stream, T? value);
}

internal sealed class DelayedNbtConverter<T>(PolyType.Utilities.DelayedValue<NbtConverter<T>> value) : NbtConverter<T>
{
    public override NbtTagType TagType => value.Result.TagType;
    public override bool ShouldWrite(T? item) => value.Result.ShouldWrite(item);
    public override NbtTagType GetTagType(T? item) => value.Result.GetTagType(item);
    public override T? ReadPayload(Stream stream, NbtTagType actualType) => value.Result.ReadPayload(stream, actualType);
    public override T? ReadPayload(Stream stream) => value.Result.ReadPayload(stream);
    public override void WritePayload(Stream stream, T? item) => value.Result.WritePayload(stream, item);
}

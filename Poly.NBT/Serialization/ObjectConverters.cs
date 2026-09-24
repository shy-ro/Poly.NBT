using PolyType.Abstractions;

namespace Poly.NBT.Serialization;

internal abstract class NbtPropertyConverter<TDeclaring>(string name, int position)
{
    public string Name { get; } = name;
    public int Position { get; } = position;
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }
    public abstract void ReadPayload(Stream stream, NbtTagType actualType, ref TDeclaring target, int depth);
    public abstract void Write(Stream stream, ref TDeclaring target, int depth);
}

internal sealed class NbtPropertyConverter<TDeclaring, TProperty> : NbtPropertyConverter<TDeclaring>
{
    private readonly NbtSerializer _serializer;
    private readonly NbtConverter<TProperty> _converter;
    private readonly Getter<TDeclaring, TProperty>? _getter;
    private readonly Setter<TDeclaring, TProperty>? _setter;
    private readonly byte[]? _cachedHeader;

    public NbtPropertyConverter(NbtSerializer serializer, IPropertyShape<TDeclaring, TProperty> property, NbtConverter<TProperty> converter)
        : base(property.Name, property.Position)
    {
        _serializer = serializer;
        _converter = converter;
        if (property.HasGetter) _getter = property.GetGetter();
        if (property.HasSetter) _setter = property.GetSetter();
        if (converter.TagType != NbtTagType.End)
        {
            using var header = new MemoryStream();
            header.WriteByte((byte)converter.TagType);
            serializer.Strings.Write(header, property.Name);
            _cachedHeader = header.ToArray();
        }
    }

    public NbtPropertyConverter(NbtSerializer serializer, IParameterShape<TDeclaring, TProperty> parameter, NbtConverter<TProperty> converter)
        : base(parameter.Name, parameter.Position)
    {
        _serializer = serializer;
        _converter = converter;
        _setter = parameter.GetSetter();
    }

    public override bool HasGetter => _getter is not null;
    public override bool HasSetter => _setter is not null;

    public override void ReadPayload(Stream stream, NbtTagType actualType, ref TDeclaring target, int depth)
    {
        TProperty? value = _converter.ReadPayload(stream, actualType, depth);
        (_setter ?? throw new InvalidOperationException()).Invoke(ref target, value!);
    }

    public override void Write(Stream stream, ref TDeclaring target, int depth)
    {
        TProperty value = (_getter ?? throw new InvalidOperationException()).Invoke(ref target);
        if (!_converter.ShouldWrite(value)) return;
        NbtTagType tagType = _converter.GetTagType(value);
        if (_cachedHeader is not null && tagType == _converter.TagType) stream.Write(_cachedHeader);
        else
        {
            stream.WriteByte((byte)tagType);
            _serializer.Strings.Write(stream, Name);
        }
        _converter.WritePayload(stream, value, depth);
    }
}

internal class NbtObjectConverter<T> : NbtConverter<T>
{
    protected readonly NbtSerializer Serializer;
    private readonly NbtPropertyConverter<T>[] _propertiesToWrite;
    protected readonly Dictionary<string, NbtPropertyConverter<T>> PropertiesToRead;

    public NbtObjectConverter(NbtSerializer serializer, NbtPropertyConverter<T>[] properties)
    {
        Serializer = serializer;
        _propertiesToWrite = properties.Where(property => property.HasGetter).ToArray();
        PropertiesToRead = properties.Where(property => property.HasSetter).ToDictionary(property => property.Name, StringComparer.Ordinal);
    }

    public override NbtTagType TagType => NbtTagType.Compound;

    public override T? ReadPayload(Stream stream, int depth) => throw new NotSupportedException($"Type {typeof(T)} has no usable constructor.");

    public sealed override void WritePayload(Stream stream, T? value, int depth)
    {
        if (value is null) throw new InvalidDataException("NBT has no null compound value.");
        foreach (NbtPropertyConverter<T> property in _propertiesToWrite) property.Write(stream, ref value, Serializer.Descend(depth));
        stream.WriteByte((byte)NbtTagType.End);
    }

    protected void ReadProperties(Stream stream, ref T result, int depth)
    {
        HashSet<int> seen = [];
        while (Serializer.ReadTagType(stream) is { } type && type != NbtTagType.End)
        {
            string name = Serializer.Strings.Read(stream);
            if (!PropertiesToRead.TryGetValue(name, out NbtPropertyConverter<T>? property))
            {
                Serializer.SkipPayload(stream, type, Serializer.Descend(depth));
                continue;
            }

            if (!seen.Add(property.Position)) throw new InvalidDataException($"Duplicate NBT property '{name}'.");
            property.ReadPayload(stream, type, ref result, Serializer.Descend(depth));
        }
    }
}

internal sealed class NbtDefaultObjectConverter<T>(
    NbtSerializer serializer,
    NbtPropertyConverter<T>[] properties,
    Func<T> constructor) : NbtObjectConverter<T>(serializer, properties)
{
    public override T ReadPayload(Stream stream, int depth)
    {
        T result = constructor();
        ReadProperties(stream, ref result, depth);
        return result;
    }
}

internal sealed class NbtParameterizedObjectConverter<T, TArgumentState>(
    NbtSerializer serializer,
    NbtPropertyConverter<T>[] properties,
    NbtPropertyConverter<TArgumentState>[] parameters,
    Func<TArgumentState> createState,
    Constructor<TArgumentState, T> constructor) : NbtObjectConverter<T>(serializer, properties)
    where TArgumentState : IArgumentState
{
    private readonly Dictionary<string, NbtPropertyConverter<TArgumentState>> _parameters = parameters.ToDictionary(item => item.Name, StringComparer.Ordinal);

    public override T ReadPayload(Stream stream, int depth)
    {
        TArgumentState state = createState();
        while (Serializer.ReadTagType(stream) is { } type && type != NbtTagType.End)
        {
            string name = Serializer.Strings.Read(stream);
            if (!_parameters.TryGetValue(name, out NbtPropertyConverter<TArgumentState>? parameter))
            {
                Serializer.SkipPayload(stream, type, Serializer.Descend(depth));
                continue;
            }

            if (state.IsArgumentSet(parameter.Position)) throw new InvalidDataException($"Duplicate NBT property '{name}'.");
            parameter.ReadPayload(stream, type, ref state, Serializer.Descend(depth));
        }

        if (!state.AreRequiredArgumentsSet) throw new InvalidDataException($"Required constructor arguments for {typeof(T)} are missing.");
        return constructor(ref state);
    }
}

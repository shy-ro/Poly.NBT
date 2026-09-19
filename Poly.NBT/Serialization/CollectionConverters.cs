using PolyType.Abstractions;

namespace Poly.NBT.Serialization;

internal class NbtEnumerableConverter<TEnumerable, TElement> : NbtConverter<TEnumerable>
{
    private readonly NbtSerializer _serializer;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable;
    private readonly bool _optimize;
    protected readonly NbtConverter<TElement> ElementConverter;

    public NbtEnumerableConverter(NbtSerializer serializer, NbtConverter<TElement> elementConverter, Func<TEnumerable, IEnumerable<TElement>> getEnumerable, bool optimize = false)
    {
        _serializer = serializer;
        ElementConverter = elementConverter;
        _getEnumerable = getEnumerable;
        Type elementType = Nullable.GetUnderlyingType(typeof(TElement)) ?? typeof(TElement);
        _optimize = optimize && serializer.Options.OptimizePrimitiveListsToArrays && (elementType == typeof(byte) || elementType == typeof(sbyte) || elementType == typeof(int) || (elementType == typeof(long) && serializer.Options.SupportsLongArray));
    }

    public override NbtTagType TagType => NbtTagType.List;
    public override NbtTagType GetTagType(TEnumerable? value) => _optimize ? OptimizedTag : NbtTagType.List;

    public override TEnumerable? ReadPayload(Stream stream) => throw new NotSupportedException($"Collection {typeof(TEnumerable)} cannot be constructed.");
    protected TElement[] ReadOptimizedElements(Stream stream, NbtTagType actualType)
    {
        if (actualType != OptimizedTag) throw new InvalidDataException($"The {actualType} tag is incompatible with {typeof(TElement)} elements.");
        int count = _serializer.Lengths.ReadCollectionLength(stream);
        TElement[] result = new TElement[count];
        for (int i = 0; i < count; i++)
        {
            Type elementType = Nullable.GetUnderlyingType(typeof(TElement)) ?? typeof(TElement);
            object value;
            if (elementType == typeof(byte)) value = NbtSerializer.ReadByte(stream);
            else if (elementType == typeof(sbyte)) value = (sbyte)NbtSerializer.ReadByte(stream);
            else if (elementType == typeof(int)) value = _serializer.Numeric.ReadInt32(stream);
            else value = _serializer.Numeric.ReadInt64(stream);
            result[i] = ToElement(value);
        }
        return result;
    }

    public sealed override void WritePayload(Stream stream, TEnumerable? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null list value.");
        IEnumerable<TElement> enumerable = _getEnumerable(value);
        IReadOnlyCollection<TElement> materialized;
        if (enumerable is IReadOnlyCollection<TElement> readOnlyCollection)
        {
            materialized = readOnlyCollection;
        }
        else if (enumerable is ICollection<TElement> collection)
        {
            materialized = new CollectionView<TElement>(collection);
        }
        else
        {
            materialized = enumerable.ToArray();
        }

        if (_optimize)
        {
            _serializer.Lengths.WriteCollectionLength(stream, materialized.Count);
            foreach (TElement item in materialized)
            {
                if (!ElementConverter.ShouldWrite(item)) throw new InvalidDataException("NBT lists cannot contain absent or null elements.");
                switch (item)
                {
                    case byte b: stream.WriteByte(b); break;
                    case sbyte sb: stream.WriteByte(unchecked((byte)sb)); break;
                    case int i: _serializer.Numeric.WriteInt32(stream, i); break;
                    case long l: _serializer.Numeric.WriteInt64(stream, l); break;
                    default: throw new InvalidDataException("Unsupported optimized primitive list element.");
                }
            }
            return;
        }

        using IEnumerator<TElement> enumerator = materialized.GetEnumerator();
        bool hasValue = enumerator.MoveNext();
        NbtTagType elementType = hasValue ? ElementConverter.GetTagType(enumerator.Current) : NbtTagType.End;
        stream.WriteByte((byte)elementType);
        _serializer.Lengths.WriteCollectionLength(stream, materialized.Count);
        while (hasValue)
        {
            TElement item = enumerator.Current;
            if (!ElementConverter.ShouldWrite(item)) throw new InvalidDataException("NBT lists cannot contain absent or null elements.");
            NbtSerializer.EnsureTagType(elementType, ElementConverter.GetTagType(item));
            ElementConverter.WritePayload(stream, item);
            hasValue = enumerator.MoveNext();
        }
    }

    private static TElement ToElement(object value)
    {
        Type type = typeof(TElement);
        if (type == typeof(byte?)) return (TElement)(object)(byte?)(byte)value;
        if (type == typeof(sbyte?)) return (TElement)(object)(sbyte?)(sbyte)value;
        if (type == typeof(int?)) return (TElement)(object)(int?)(int)value;
        if (type == typeof(long?)) return (TElement)(object)(long?)(long)value;
        return (TElement)value;
    }

    private NbtTagType OptimizedTag => (Nullable.GetUnderlyingType(typeof(TElement)) ?? typeof(TElement)) switch
    {
        Type type when type == typeof(byte) || type == typeof(sbyte) => NbtTagType.ByteArray,
        Type type when type == typeof(int) => NbtTagType.IntArray,
        Type type when type == typeof(long) => NbtTagType.LongArray,
        _ => NbtTagType.List,
    };

    protected (int Count, NbtTagType ElementType) ReadHeader(Stream stream)
    {
        NbtTagType elementType = _serializer.ReadTagType(stream);
        int count = _serializer.Lengths.ReadCollectionLength(stream);
        if (count == 0)
        {
            if (elementType != NbtTagType.End && ElementConverter.TagType != NbtTagType.End && elementType != ElementConverter.TagType)
                throw new InvalidDataException("An empty NBT list has an incompatible element type.");
        }
        else
        {
            if (ElementConverter.TagType != NbtTagType.End) NbtSerializer.EnsureTagType(ElementConverter.TagType, elementType);
        }

        return (count, elementType);
    }

    private sealed class CollectionView<T>(ICollection<T> collection) : IReadOnlyCollection<T>
    {
        public int Count => collection.Count;
        public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

internal sealed class NbtMutableEnumerableConverter<TEnumerable, TElement>(
    NbtSerializer serializer,
    NbtConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    MutableCollectionConstructor<TElement, TEnumerable> constructor,
    EnumerableAppender<TEnumerable, TElement> appender, bool optimize = false) : NbtEnumerableConverter<TEnumerable, TElement>(serializer, elementConverter, getEnumerable, optimize)
{
    public override TEnumerable ReadPayload(Stream stream, NbtTagType actualType)
    {
        if (actualType is NbtTagType.ByteArray or NbtTagType.IntArray or NbtTagType.LongArray)
        {
            TEnumerable result = constructor(new());
            foreach (TElement value in ReadOptimizedElements(stream, actualType)) appender(ref result, value);
            return result;
        }
        return base.ReadPayload(stream, actualType)!;
    }

    public override TEnumerable ReadPayload(Stream stream)
    {
        (int count, NbtTagType elementType) = ReadHeader(stream);
        TEnumerable result = constructor(new() { Capacity = count });
        for (int index = 0; index < count; index++)
        {
            if (!appender(ref result, ElementConverter.ReadPayload(stream, elementType)!)) throw new InvalidDataException("Could not append an NBT list element.");
        }
        return result;
    }
}

internal sealed class NbtParameterizedEnumerableConverter<TEnumerable, TElement>(
    NbtSerializer serializer,
    NbtConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor, bool optimize = false) : NbtEnumerableConverter<TEnumerable, TElement>(serializer, elementConverter, getEnumerable, optimize)
{
    public override TEnumerable ReadPayload(Stream stream, NbtTagType actualType)
        => actualType is NbtTagType.ByteArray or NbtTagType.IntArray or NbtTagType.LongArray
            ? constructor(ReadOptimizedElements(stream, actualType))
            : base.ReadPayload(stream, actualType)!;

    public override TEnumerable ReadPayload(Stream stream)
    {
        (int count, NbtTagType elementType) = ReadHeader(stream);
        TElement[] values = new TElement[count];
        for (int index = 0; index < values.Length; index++) values[index] = ElementConverter.ReadPayload(stream, elementType)!;
        return constructor(values);
    }
}

internal class NbtDictionaryConverter<TDictionary, TValue> : NbtConverter<TDictionary>
{
    private readonly NbtSerializer _serializer;
    private readonly Func<TDictionary, IReadOnlyDictionary<string, TValue>> _getDictionary;
    protected readonly NbtConverter<TValue> ValueConverter;

    public NbtDictionaryConverter(NbtSerializer serializer, NbtConverter<TValue> valueConverter, Func<TDictionary, IReadOnlyDictionary<string, TValue>> getDictionary)
    {
        _serializer = serializer;
        ValueConverter = valueConverter;
        _getDictionary = getDictionary;
    }

    public override NbtTagType TagType => NbtTagType.Compound;
    public override TDictionary? ReadPayload(Stream stream) => throw new NotSupportedException($"Dictionary {typeof(TDictionary)} cannot be constructed.");

    public sealed override void WritePayload(Stream stream, TDictionary? value)
    {
        if (value is null) throw new InvalidDataException("NBT has no null compound value.");
        foreach ((string name, TValue item) in _getDictionary(value))
        {
            if (!ValueConverter.ShouldWrite(item)) continue;
            stream.WriteByte((byte)ValueConverter.GetTagType(item));
            _serializer.Strings.Write(stream, name);
            ValueConverter.WritePayload(stream, item);
        }
        stream.WriteByte((byte)NbtTagType.End);
    }

    protected IEnumerable<KeyValuePair<string, TValue>> ReadEntries(Stream stream)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        while (_serializer.ReadTagType(stream) is { } type && type != NbtTagType.End)
        {
            string key = _serializer.Strings.Read(stream);
            if (!seen.Add(key)) throw new InvalidDataException($"Duplicate NBT key '{key}'.");
            yield return new(key, ValueConverter.ReadPayload(stream, type)!);
        }
    }
}

internal sealed class NbtMutableDictionaryConverter<TDictionary, TValue>(
    NbtSerializer serializer,
    NbtConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<string, TValue>> getDictionary,
    MutableCollectionConstructor<string, TDictionary> constructor,
    DictionaryInserter<TDictionary, string, TValue> inserter) : NbtDictionaryConverter<TDictionary, TValue>(serializer, valueConverter, getDictionary)
{
    public override TDictionary ReadPayload(Stream stream)
    {
        TDictionary result = constructor();
        foreach ((string key, TValue value) in ReadEntries(stream)) inserter(ref result, key, value);
        return result;
    }
}

internal sealed class NbtParameterizedDictionaryConverter<TDictionary, TValue>(
    NbtSerializer serializer,
    NbtConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<string, TValue>> getDictionary,
    ParameterizedCollectionConstructor<string, KeyValuePair<string, TValue>, TDictionary> constructor) : NbtDictionaryConverter<TDictionary, TValue>(serializer, valueConverter, getDictionary)
{
    public override TDictionary ReadPayload(Stream stream) => constructor(ReadEntries(stream).ToArray());
}

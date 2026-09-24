using System.Diagnostics.CodeAnalysis;
using Poly.NBT.Dom;
using Poly.NBT.Internal;
using Poly.NBT.Serialization;
using PolyType;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.Utilities;

namespace Poly.NBT;

/// <summary>A configured, thread-safe NBT serializer.</summary>
public sealed partial class NbtSerializer
{
    private readonly MultiProviderTypeCache _converterCache;
    private readonly IReadOnlyDictionary<Type, NbtConverter> _builtIns;
    private readonly int _maxDepth;

    private NbtSerializer(NbtOptions options)
    {
        Validate(options);
        Options = options;
        _maxDepth = options.EffectiveMaxDepth;
        Numeric = options.NumericEncoding switch
        {
            NbtNumericEncoding.VarIntZigZag => new VarIntNumericCodec(),
            NbtNumericEncoding.Fixed when options.Endianness == NbtEndianness.BigEndian => new BigEndianNumericCodec(),
            NbtNumericEncoding.Fixed => new LittleEndianNumericCodec(),
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        };
        Lengths = options.NumericEncoding == NbtNumericEncoding.VarIntZigZag
            ? new VarIntLengthCodec(options.EffectiveMaxCollectionLength)
            : new FixedLengthCodec(Numeric, options.EffectiveMaxCollectionLength);
        Strings = new NbtStringCodec(options.StringEncoding, Lengths);
        _builtIns = CreateBuiltIns();
        _converterCache = new MultiProviderTypeCache
        {
            DelayedValueFactory = new DelayedConverterFactory(),
            ValueBuilderFactory = context => new Builder(context, this),
        };
    }

    public NbtOptions Options { get; }
    internal NbtNumericCodec Numeric { get; }
    internal NbtLengthCodec Lengths { get; }
    internal NbtStringCodec Strings { get; }

    public static NbtSerializer Create(NbtOptions options) => new(options);

    public void Serialize<T>(Stream destination, T? value, string rootTagName, ITypeShape<T> shape)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(rootTagName);
        ArgumentNullException.ThrowIfNull(shape);
        NbtConverter<T> converter = GetConverter(shape);
        SerializeRoot(destination, value, rootTagName, converter);
    }

    public void Serialize(Stream destination, NbtDocument document)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(document);
        SerializeRoot(destination, document.RootElement, document.RootTagName, GetElementConverter());
    }

    private void SerializeRoot<T>(Stream destination, T? value, string rootTagName, NbtConverter<T> converter)
    {
        if (!converter.ShouldWrite(value)) throw new InvalidDataException("The root NBT value cannot be absent.");
        WriteRootHeader(destination, converter.GetTagType(value), rootTagName);
        converter.WritePayload(destination, value, RootDepth);
    }

    /// <summary>Writes the tag byte and, for a named dialect, the name field. See <see cref="NbtRootTagNaming"/>.</summary>
    private void WriteRootHeader(Stream destination, NbtTagType rootType, string rootTagName)
    {
        destination.WriteByte((byte)rootType);

        // The name field is part of the header, not an optional extra: Java reads it unconditionally, so
        // omitting it when the caller passed an empty name produced a document no Java reader could parse.
        // "No name field at all" is a property of the dialect, expressed by RootTagNaming.
        if (Options.RootTagNaming == NbtRootTagNaming.Named)
        {
            Strings.Write(destination, rootTagName);
        }
    }

    /// <summary>Deserializes one root tag. Object-typed scalars become CLR primitives; object-typed lists and compounds remain DOM values.</summary>
    /// <param name="source">The stream holding one root tag, positioned at its tag byte.</param>
    /// <param name="shape">The shape of the value to materialize.</param>
    /// <param name="rootNameOmitted">
    /// Accepts a non-standard stream that has no name field even though <see cref="NbtOptions.RootTagNaming"/> is
    /// <see cref="NbtRootTagNaming.Named"/>. Leave it <see langword="false"/> for anything a Java or Bedrock
    /// implementation wrote; the dialect already governs whether the field exists.
    /// </param>
    public T? Deserialize<T>(Stream source, ITypeShape<T> shape, bool rootNameOmitted = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(shape);
        NbtConverter<T> converter = GetConverter(shape);
        (NbtTagType actual, _) = ReadRootHeader(source, rootNameOmitted);
        return converter.ReadPayload(source, actual, RootDepth);
    }

    /// <param name="source">The stream holding one root tag, positioned at its tag byte.</param>
    /// <param name="rootNameOmitted">As described on <see cref="Deserialize{T}(Stream, ITypeShape{T}, bool)"/>.</param>
    public NbtDocument DeserializeDocument(Stream source, bool rootNameOmitted = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        (NbtTagType actual, string rootTagName) = ReadRootHeader(source, rootNameOmitted);
        NbtElement rootElement = GetElementConverter().ReadPayload(source, actual, RootDepth)
            ?? throw new InvalidDataException("The root NBT element cannot be null.");
        return new(rootTagName, rootElement);
    }

    public byte[] Serialize<T>(T? value, string rootTagName, ITypeShape<T> shape)
    {
        using var stream = new MemoryStream();
        Serialize(stream, value, rootTagName, shape);
        return stream.ToArray();
    }

    public byte[] Serialize(NbtDocument document)
    {
        using var stream = new MemoryStream();
        Serialize(stream, document);
        return stream.ToArray();
    }

    public T? Deserialize<T>(ReadOnlySpan<byte> data, ITypeShape<T> shape, bool rootNameOmitted = false)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        T? result = Deserialize(stream, shape, rootNameOmitted);
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing data follows the root NBT tag.");
        return result;
    }

    public NbtDocument DeserializeDocument(ReadOnlySpan<byte> data, bool rootNameOmitted = false)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        NbtDocument result = DeserializeDocument(stream, rootNameOmitted);
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing data follows the root NBT tag.");
        return result;
    }

    public void Serialize<T>(Stream destination, T? value, string rootTagName) where T : IShapeable<T>
        => Serialize(destination, value, rootTagName, T.GetTypeShape());

    public T? Deserialize<T>(Stream source, bool rootNameOmitted = false) where T : IShapeable<T>
        => Deserialize(source, T.GetTypeShape(), rootNameOmitted);

    [RequiresUnreferencedCode("The PolyType reflection provider requires unreferenced code.")]
    [RequiresDynamicCode("The PolyType reflection provider requires dynamic code.")]
    public byte[] SerializeUsingReflection<T>(T? value, string rootTagName)
        => Serialize(value, rootTagName, ReflectionTypeShapeProvider.Default.GetTypeShape<T>());

    [RequiresUnreferencedCode("The PolyType reflection provider requires unreferenced code.")]
    [RequiresDynamicCode("The PolyType reflection provider requires dynamic code.")]
    public T? DeserializeUsingReflection<T>(ReadOnlySpan<byte> data, bool rootNameOmitted = false)
        => Deserialize(data, ReflectionTypeShapeProvider.Default.GetTypeShape<T>(), rootNameOmitted);

    internal NbtConverter<T> GetConverter<T>(ITypeShape<T> shape) => (NbtConverter<T>)_converterCache.GetOrAdd(shape)!;
    internal NbtConverter GetConverter(Type type, ITypeShapeProvider provider) => (NbtConverter)_converterCache.GetOrAdd(type, provider)!;

    internal NbtTagType ReadTagType(Stream stream)
    {
        int value = stream.ReadByte();
        if (value < 0) throw new EndOfStreamException();
        if (value > (byte)NbtTagType.LongArray) throw new FormatException($"Unknown NBT tag type {value}.");
        return (NbtTagType)value;
    }

    internal static void EnsureTagType(NbtTagType expected, NbtTagType actual)
    {
        if (actual != expected) throw new InvalidDataException($"Expected {expected}, but found {actual}.");
    }

    private NbtConverter<NbtElement> GetElementConverter() => (NbtConverter<NbtElement>)_builtIns[typeof(NbtElement)];

    /// <summary>The nesting level of the root tag. Every child level is derived from it by <see cref="Descend"/>.</summary>
    private const int RootDepth = 1;

    /// <summary>
    /// Returns the nesting level of a child of the value at <paramref name="depth"/>, or throws when that would
    /// exceed <see cref="NbtOptions.MaxDepth"/>.
    /// </summary>
    /// <remarks>
    /// The readers and writers are recursive, and their recursion is driven by the input: a few kilobytes of
    /// nested <c>TAG_List</c> headers, or a user-built tree of nested lists, is enough to exhaust the stack.
    /// <see cref="StackOverflowException"/> cannot be caught in .NET, so bounding the recursion here is the only
    /// way to fail recoverably.
    /// </remarks>
    internal int Descend(int depth)
    {
        int next = depth + 1;
        return next > _maxDepth
            ? throw new InvalidDataException($"The NBT document nests more than {_maxDepth} levels deep; raise NbtOptions.MaxDepth to accept it.")
            : next;
    }

    internal NbtElement ToElementInternal<T>(T? value, ITypeShape<T> shape)
    {
        using var stream = new MemoryStream();
        Serialize(stream, value, "", shape);
        stream.Position = 0;

        // Read the root header rather than just the tag byte: in a named dialect the header carries a name
        // field, and the converter would otherwise start on the name's length bytes and see TAG_End.
        (NbtTagType rootType, _) = ReadRootHeader(stream, rootNameOmitted: false);
        return GetElementConverter().ReadPayload(stream, rootType, RootDepth)
            ?? throw new InvalidDataException("The root NBT value cannot be absent.");
    }

    internal T? FromElementInternal<T>(NbtElement element, ITypeShape<T> shape)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var stream = new MemoryStream();
        NbtConverter<NbtElement> elementConverter = GetElementConverter();
        WriteRootHeader(stream, elementConverter.GetTagType(element), string.Empty);
        elementConverter.WritePayload(stream, element, RootDepth);
        stream.Position = 0;
        return Deserialize(stream, shape);
    }

    private (NbtTagType Type, string RootTagName) ReadRootHeader(Stream source, bool rootNameOmitted)
    {
        NbtTagType type = ReadTagType(source);
        string rootTagName = Options.RootTagNaming == NbtRootTagNaming.Named && !rootNameOmitted
            ? Strings.Read(source)
            : string.Empty;
        return (type, rootTagName);
    }

    internal void SkipPayload(Stream stream, NbtTagType type, int depth)
    {
        switch (type)
        {
            case NbtTagType.Byte: ReadByte(stream); break;
            case NbtTagType.Short: Numeric.ReadInt16(stream); break;
            case NbtTagType.Int: Numeric.ReadInt32(stream); break;
            case NbtTagType.Long: Numeric.ReadInt64(stream); break;
            case NbtTagType.Float: Numeric.ReadSingle(stream); break;
            case NbtTagType.Double: Numeric.ReadDouble(stream); break;
            case NbtTagType.String: SkipBytes(stream, Lengths.ReadStringLength(stream)); break;
            case NbtTagType.ByteArray: SkipBytes(stream, Lengths.ReadCollectionLength(stream)); break;
            case NbtTagType.IntArray:
                if (Numeric is VarIntNumericCodec)
                    for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) Numeric.ReadInt32(stream);
                else SkipBytes(stream, checked(Lengths.ReadCollectionLength(stream) * sizeof(int)));
                break;
            case NbtTagType.LongArray:
                if (Numeric is VarIntNumericCodec)
                    for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) Numeric.ReadInt64(stream);
                else SkipBytes(stream, checked(Lengths.ReadCollectionLength(stream) * sizeof(long)));
                break;
            case NbtTagType.List:
                NbtTagType elementType = ReadTagType(stream);
                for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) SkipPayload(stream, elementType, Descend(depth));
                break;
            case NbtTagType.Compound:
                while ((type = ReadTagType(stream)) != NbtTagType.End)
                {
                    _ = Strings.Read(stream);
                    SkipPayload(stream, type, Descend(depth));
                }
                break;
            case NbtTagType.End:
                break;
            default:
                throw new FormatException($"Unknown NBT tag type {type}.");
        }
    }

    private static void SkipBytes(Stream stream, int count)
    {
        if (stream.CanSeek)
        {
            long target = checked(stream.Position + count);
            if (target > stream.Length) throw new EndOfStreamException();
            stream.Position = target;
            return;
        }

        Span<byte> buffer = stackalloc byte[256];
        while (count > 0)
        {
            int current = Math.Min(count, buffer.Length);
            stream.ReadExactly(buffer[..current]);
            count -= current;
        }
    }

    internal static byte ReadByte(Stream stream)
    {
        int value = stream.ReadByte();
        return value >= 0 ? (byte)value : throw new EndOfStreamException();
    }

    private IReadOnlyDictionary<Type, NbtConverter> CreateBuiltIns()
    {
        NbtConverter[] converters =
        [
            new PrimitiveConverter<bool>(NbtTagType.Byte, stream => ReadByte(stream) != 0, (stream, value) => stream.WriteByte(value ? (byte)1 : (byte)0)),
            new PrimitiveConverter<byte>(NbtTagType.Byte, ReadByte, (stream, value) => stream.WriteByte(value)),
            new PrimitiveConverter<sbyte>(NbtTagType.Byte, stream => unchecked((sbyte)ReadByte(stream)), (stream, value) => stream.WriteByte(unchecked((byte)value))),
            new PrimitiveConverter<short>(NbtTagType.Short, Numeric.ReadInt16, Numeric.WriteInt16),
            new PrimitiveConverter<int>(NbtTagType.Int, Numeric.ReadInt32, Numeric.WriteInt32),
            new PrimitiveConverter<long>(NbtTagType.Long, Numeric.ReadInt64, Numeric.WriteInt64),
            new PrimitiveConverter<float>(NbtTagType.Float, Numeric.ReadSingle, Numeric.WriteSingle),
            new PrimitiveConverter<double>(NbtTagType.Double, Numeric.ReadDouble, Numeric.WriteDouble),
            new StringConverter(Strings),
            new ByteArrayConverter(Lengths, Options.OptimizePrimitiveListsToArrays),
            new SByteArrayConverter(Lengths, Options.OptimizePrimitiveListsToArrays),
            new IntArrayConverter(Lengths, Numeric, Options.OptimizePrimitiveListsToArrays),
            new LongArrayConverter(Lengths, Numeric, Options.SupportsLongArray, Options.OptimizePrimitiveListsToArrays),
            new FloatArrayConverter(Lengths, Numeric),
            new DoubleArrayConverter(Lengths, Numeric),
            new NbtElementConverter(this),
        ];
        return converters.ToDictionary(converter => converter.Type);
    }

    private static void Validate(NbtOptions options)
    {
        if (!Enum.IsDefined(options.Endianness)) throw new ArgumentOutOfRangeException(nameof(options.Endianness));
        if (!Enum.IsDefined(options.StringEncoding)) throw new ArgumentOutOfRangeException(nameof(options.StringEncoding));
        if (!Enum.IsDefined(options.NumericEncoding)) throw new ArgumentOutOfRangeException(nameof(options.NumericEncoding));
        if (!Enum.IsDefined(options.RootTagNaming)) throw new ArgumentOutOfRangeException(nameof(options.RootTagNaming));
        if (options.MaxDepth < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxDepth));
        if (options.MaxCollectionLength < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxCollectionLength));
    }

    private sealed class DelayedConverterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> _) => new DelayedValue<NbtConverter<T>>(self => new DelayedNbtConverter<T>(self));
    }
}

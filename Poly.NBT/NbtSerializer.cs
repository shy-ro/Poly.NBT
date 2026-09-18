using System.Diagnostics.CodeAnalysis;
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

    private NbtSerializer(NbtOptions options)
    {
        Validate(options);
        Options = options;
        Numeric = options.NumericEncoding switch
        {
            NbtNumericEncoding.VarIntZigZag => new VarIntNumericCodec(),
            NbtNumericEncoding.Fixed when options.Endianness == NbtEndianness.BigEndian => new BigEndianNumericCodec(),
            NbtNumericEncoding.Fixed => new LittleEndianNumericCodec(),
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        };
        Lengths = options.NumericEncoding == NbtNumericEncoding.VarIntZigZag
            ? new VarIntLengthCodec()
            : new FixedLengthCodec(Numeric);
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

    public static NbtSerializer Create(NbtOptions options = default) => new(options);

    public void Serialize<T>(Stream destination, T? value, string rootTagName, ITypeShape<T> shape)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(rootTagName);
        ArgumentNullException.ThrowIfNull(shape);
        NbtConverter<T> converter = GetConverter(shape);
        if (!converter.ShouldWrite(value)) throw new InvalidDataException("The root NBT value cannot be absent.");

        NbtTagType rootType = converter.GetTagType(value);
        destination.WriteByte((byte)rootType);
        if (Options.RootTagNaming == NbtRootTagNaming.Named && rootTagName.Length != 0)
        {
            Strings.Write(destination, rootTagName);
        }

        converter.WritePayload(destination, value);
    }

    public T? Deserialize<T>(Stream source, ITypeShape<T> shape, bool rootNameOmitted = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(shape);
        NbtConverter<T> converter = GetConverter(shape);
        NbtTagType actual = ReadTagType(source);
        if (Options.RootTagNaming == NbtRootTagNaming.Named && !rootNameOmitted)
        {
            _ = Strings.Read(source);
        }

        return converter.ReadPayload(source, actual);
    }

    public byte[] Serialize<T>(T? value, string rootTagName, ITypeShape<T> shape)
    {
        using var stream = new MemoryStream();
        Serialize(stream, value, rootTagName, shape);
        return stream.ToArray();
    }

    public T? Deserialize<T>(ReadOnlySpan<byte> data, ITypeShape<T> shape, bool rootNameOmitted = false)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        T? result = Deserialize(stream, shape, rootNameOmitted);
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing data follows the root NBT tag.");
        return result;
    }

    public void Serialize<T>(Stream destination, T? value, string rootTagName = "root") where T : IShapeable<T>
        => Serialize(destination, value, rootTagName, T.GetTypeShape());

    public T? Deserialize<T>(Stream source, bool rootNameOmitted = false) where T : IShapeable<T>
        => Deserialize(source, T.GetTypeShape(), rootNameOmitted);

    [RequiresUnreferencedCode("The PolyType reflection provider requires unreferenced code.")]
    [RequiresDynamicCode("The PolyType reflection provider requires dynamic code.")]
    public byte[] SerializeUsingReflection<T>(T? value, string rootTagName = "root")
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

    internal void SkipPayload(Stream stream, NbtTagType type)
    {
        switch (type)
        {
            case NbtTagType.Byte: ReadByte(stream); break;
            case NbtTagType.Short: Numeric.ReadInt16(stream); break;
            case NbtTagType.Int: Numeric.ReadInt32(stream); break;
            case NbtTagType.Long: Numeric.ReadInt64(stream); break;
            case NbtTagType.Float: Numeric.ReadSingle(stream); break;
            case NbtTagType.Double: Numeric.ReadDouble(stream); break;
            case NbtTagType.String: Strings.Read(stream); break;
            case NbtTagType.ByteArray: StreamIO.ReadExactly(stream, Lengths.ReadCollectionLength(stream)); break;
            case NbtTagType.IntArray:
                for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) Numeric.ReadInt32(stream);
                break;
            case NbtTagType.LongArray:
                for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) Numeric.ReadInt64(stream);
                break;
            case NbtTagType.List:
                NbtTagType elementType = ReadTagType(stream);
                for (int count = Lengths.ReadCollectionLength(stream); count > 0; count--) SkipPayload(stream, elementType);
                break;
            case NbtTagType.Compound:
                while ((type = ReadTagType(stream)) != NbtTagType.End)
                {
                    _ = Strings.Read(stream);
                    SkipPayload(stream, type);
                }
                break;
            case NbtTagType.End:
                break;
            default:
                throw new FormatException($"Unknown NBT tag type {type}.");
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
            new ByteArrayConverter(Lengths),
            new SByteArrayConverter(Lengths),
            new IntArrayConverter(Lengths, Numeric),
            new LongArrayConverter(Lengths, Numeric, Options.SupportsLongArray),
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
    }

    private sealed class DelayedConverterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> _) => new DelayedValue<NbtConverter<T>>(self => new DelayedNbtConverter<T>(self));
    }
}

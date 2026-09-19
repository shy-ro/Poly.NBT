namespace Poly.NBT;

public enum NbtEndianness : byte
{
    BigEndian = 0,
    LittleEndian = 1,
}

public enum NbtStringEncoding : byte
{
    ModifiedUtf8 = 0,
    Utf8 = 1,
    Utf8WithEscapes = 2,
}

public enum NbtNumericEncoding : byte
{
    Fixed = 0,
    VarIntZigZag = 1,
}

public enum NbtRootTagNaming : byte
{
    Named = 0,
    Omitted = 1,
}

/// <summary>Controls the NBT wire-format dialect.</summary>
public readonly record struct NbtOptions
{
    private readonly byte _optimizePrimitiveListsToArrays;

    public NbtEndianness Endianness { get; init; }
    public NbtStringEncoding StringEncoding { get; init; }
    public NbtNumericEncoding NumericEncoding { get; init; }
    public NbtRootTagNaming RootTagNaming { get; init; }
    public bool SupportsLongArray { get; init; }
    public bool OptimizePrimitiveListsToArrays
    {
        get => _optimizePrimitiveListsToArrays != 2;
        init => _optimizePrimitiveListsToArrays = value ? (byte)1 : (byte)2;
    }

    public static readonly NbtOptions JavaEdition = new()
    {
        SupportsLongArray = true,
    };

    public static readonly NbtOptions JavaNetworkEdition = JavaEdition with
    {
        RootTagNaming = NbtRootTagNaming.Omitted,
    };

    public static readonly NbtOptions BedrockEdition = new()
    {
        Endianness = NbtEndianness.LittleEndian,
        StringEncoding = NbtStringEncoding.Utf8WithEscapes,
        SupportsLongArray = false,
    };

    public static readonly NbtOptions BedrockNetworkEdition = BedrockEdition with
    {
        NumericEncoding = NbtNumericEncoding.VarIntZigZag,
        RootTagNaming = NbtRootTagNaming.Omitted,
    };
}

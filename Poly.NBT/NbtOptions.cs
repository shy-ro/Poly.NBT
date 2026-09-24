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
/// <remarks>
/// <para>
/// The four presets below are the dialects this library is meant to be used with. Every one of them enables
/// <see cref="OptimizePrimitiveListsToArrays"/>, because every real Minecraft dialect writes primitive
/// collections as the matching array tag.
/// </para>
/// <para>
/// <see cref="NbtSerializer.Create(NbtOptions)"/> also accepts <c>default(NbtOptions)</c>. That is not a preset
/// and is not intended to be one, but it is nonetheless coherent - big-endian, Modified UTF-8, fixed-width
/// numbers, a named root - and simply leaves both boolean capabilities switched off, so
/// <see cref="OptimizePrimitiveListsToArrays"/> is <see langword="false"/> there. Because the type stores that
/// flag as a plain <see langword="bool"/>, two instances compare equal exactly when their properties read the
/// same, which keeps the value a usable dictionary key and a predictable <c>with</c> result.
/// </para>
/// </remarks>
public readonly record struct NbtOptions
{
    /// <summary>The nesting limit used when <see cref="MaxDepth"/> is not set.</summary>
    public const int DefaultMaxDepth = 512;

    public NbtEndianness Endianness { get; init; }
    public NbtStringEncoding StringEncoding { get; init; }
    public NbtNumericEncoding NumericEncoding { get; init; }
    public NbtRootTagNaming RootTagNaming { get; init; }

    /// <summary>Enables <c>TAG_Long_Array</c>. Java has supported it since 1.12; Bedrock never has.</summary>
    public bool SupportsLongArray { get; init; }

    /// <summary>
    /// Writes <c>byte</c>/<c>sbyte</c>/<c>int</c>/<c>long</c> collections as the matching primitive array tag
    /// instead of a homogeneous <c>TAG_List</c>. Both layouts are legal NBT and each is read back either way;
    /// the array form is what Minecraft itself produces.
    /// </summary>
    public bool OptimizePrimitiveListsToArrays { get; init; }

    /// <summary>
    /// The maximum nesting depth a single document may reach, counting the root tag as level one. Zero selects
    /// <see cref="DefaultMaxDepth"/>.
    /// </summary>
    /// <remarks>
    /// Reading is driven by the wire format, so a small input can describe a deeply nested document: a few
    /// kilobytes of <c>TAG_List</c> headers are enough to run the recursive readers out of stack, and a stack
    /// overflow cannot be caught in .NET - the process dies. This limit turns that into an
    /// <see cref="InvalidDataException"/>. Writing is bounded the same way, so a user-built tree cannot overflow
    /// the stack either.
    /// </remarks>
    public int MaxDepth { get; init; }

    /// <summary>The nesting limit with <see cref="MaxDepth"/>'s zero sentinel resolved.</summary>
    internal int EffectiveMaxDepth => MaxDepth > 0 ? MaxDepth : DefaultMaxDepth;

    public static readonly NbtOptions JavaEdition = new()
    {
        SupportsLongArray = true,
        OptimizePrimitiveListsToArrays = true,
        MaxDepth = DefaultMaxDepth,
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
        OptimizePrimitiveListsToArrays = true,
        MaxDepth = DefaultMaxDepth,
    };

    public static readonly NbtOptions BedrockNetworkEdition = BedrockEdition with
    {
        NumericEncoding = NbtNumericEncoding.VarIntZigZag,
        RootTagNaming = NbtRootTagNaming.Omitted,
    };
}

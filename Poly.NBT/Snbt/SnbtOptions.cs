namespace Poly.NBT.Snbt;

/// <summary>Controls the accepted SNBT dialect.</summary>
/// <remarks>
/// <para>
/// Every flag below corresponds to a syntax extension of the Minecraft 1.21.5 SNBT text format, which was
/// rolled out across three snapshots of that version: heterogeneous lists in 25w04a, the numeric and string
/// extensions plus trailing commas in 25w09a, and the text operations in 25w10a. Each flag names the
/// snapshot that introduced its syntax.
/// </para>
/// <para>
/// All of them are absent from the pre-1.21.5 grammar, so <see cref="v1_13"/> disables every one of them.
/// The flag set is coarser than the snapshot timeline: a 25w04a dialect, which would accept heterogeneous
/// lists but not the 25w09a numeric forms, cannot be expressed with the two presets provided.
/// </para>
/// </remarks>
public readonly record struct SnbtOptions
{
    /// <summary>The nesting limit used when <see cref="MaxDepth"/> is not set.</summary>
    public const int DefaultMaxDepth = 512;

    /// <summary>Allows a single trailing comma after the last valid element of a list or compound (25w09a).</summary>
    public bool AllowTrailingCommas { get; init; }

    /// <summary>
    /// Allows list elements with differing NBT tag types. SNBT text accepted these from 25w04a; 25w09a extended
    /// the same allowance to every NBT-backed component, such as text components and predicates.
    /// </summary>
    public bool AllowHeterogeneousLists { get; init; }

    /// <summary>Allows <c>E</c>-notation for floating-point literals, for example <c>1.2e3</c> (25w09a).</summary>
    public bool AllowScientificNotation { get; init; }

    /// <summary>Allows the <c>0x</c> hexadecimal and <c>0b</c> binary integer prefixes (25w09a).</summary>
    public bool AllowBinaryAndHexLiterals { get; init; }

    /// <summary>Allows a fractional number to omit the whole or fractional part, for example <c>.1</c> and <c>1.</c> (25w09a).</summary>
    public bool AllowOmittedFloatParts { get; init; }

    /// <summary>
    /// Allows the <c>true</c> and <c>false</c> literals, which are bytes <c>1</c> and <c>0</c>.
    /// The wiki history does not record the version that introduced these literals, so unlike the other flags
    /// this one carries no snapshot number.
    /// </summary>
    public bool AllowBooleanLiterals { get; init; }

    /// <summary>Allows <c>_</c> between digit sequences, for example <c>1_000</c> (25w09a).</summary>
    public bool AllowUnderscoreSeparators { get; init; }

    /// <summary>Allows the <c>s</c>/<c>S</c> signed and <c>u</c>/<c>U</c> unsigned suffixes in front of an integer type suffix (25w09a).</summary>
    public bool AllowSignednessSuffixes { get; init; }

    /// <summary>Allows the <c>bool(arg)</c> and <c>uuid(str)</c> text operations (25w10a).</summary>
    public bool AllowSnbtOperations { get; init; }

    /// <summary>
    /// The maximum nesting depth a single value may reach, counting the outermost value as level one. Zero
    /// selects <see cref="DefaultMaxDepth"/>.
    /// </summary>
    /// <remarks>
    /// Unlike the other properties this one is not a syntax feature and is absent from both presets' source
    /// dialects. Both the parser and the writer recurse once per nesting level, and the level count comes
    /// straight from the input: a few kilobytes of nested brackets run the parser out of stack, and a stack
    /// overflow cannot be caught in .NET - the process dies. The limit turns that into a
    /// <see cref="SnbtParseException"/> from the parser and an <see cref="InvalidDataException"/> from the
    /// writer.
    /// </remarks>
    public int MaxDepth { get; init; }

    /// <summary>The nesting limit with <see cref="MaxDepth"/>'s zero sentinel resolved.</summary>
    internal int EffectiveMaxDepth => MaxDepth > 0 ? MaxDepth : DefaultMaxDepth;

    /// <summary>The pre-1.21.5 dialect: strict, without any of the 1.21.5 extensions.</summary>
    public static readonly SnbtOptions v1_13 = new()
    {
        MaxDepth = DefaultMaxDepth,
    };

    /// <summary>The 1.21.5 dialect: enables every extension of the 1.21.5 text format.</summary>
    public static readonly SnbtOptions v1_21_5 = new()
    {
        AllowTrailingCommas = true,
        AllowHeterogeneousLists = true,
        AllowScientificNotation = true,
        AllowBinaryAndHexLiterals = true,
        AllowOmittedFloatParts = true,
        AllowBooleanLiterals = true,
        AllowUnderscoreSeparators = true,
        AllowSignednessSuffixes = true,
        AllowSnbtOperations = true,
        MaxDepth = DefaultMaxDepth,
    };
}

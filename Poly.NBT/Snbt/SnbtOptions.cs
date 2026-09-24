namespace Poly.NBT.Snbt;

/// <summary>Controls the accepted SNBT dialect.</summary>
/// <remarks>
/// Every flag below corresponds to a syntax extension introduced by Minecraft 1.21.5 (25w09a / the 1.21.5 release).
/// <see cref="v1_13"/> disables all of them; <see cref="v1_21_5"/> enables all of them.
/// </remarks>
public readonly record struct SnbtOptions
{
    /// <summary>Allows a single trailing comma after the last valid element of a list or compound.</summary>
    public bool AllowTrailingCommas { get; init; }

    /// <summary>Allows list elements with differing NBT tag types.</summary>
    public bool AllowHeterogeneousLists { get; init; }

    /// <summary>Allows <c>E</c>-notation for floating-point literals, for example <c>1.2e3</c>.</summary>
    public bool AllowScientificNotation { get; init; }

    /// <summary>Allows the <c>0x</c> hexadecimal and <c>0b</c> binary integer prefixes.</summary>
    public bool AllowBinaryAndHexLiterals { get; init; }

    /// <summary>Allows a fractional number to omit the whole or fractional part, for example <c>.1</c> and <c>1.</c>.</summary>
    public bool AllowOmittedFloatParts { get; init; }

    /// <summary>Allows the <c>true</c> and <c>false</c> literals, which are bytes <c>1</c> and <c>0</c>.</summary>
    public bool AllowBooleanLiterals { get; init; }

    /// <summary>Allows <c>_</c> between digit sequences, for example <c>1_000</c>.</summary>
    public bool AllowUnderscoreSeparators { get; init; }

    /// <summary>Allows the <c>s</c>/<c>S</c> signed and <c>u</c>/<c>U</c> unsigned suffixes in front of an integer type suffix.</summary>
    public bool AllowSignednessSuffixes { get; init; }

    /// <summary>Allows the <c>bool(arg)</c> and <c>uuid(str)</c> text operations.</summary>
    public bool AllowSnbtOperations { get; init; }

    /// <summary>The classic 1.13 dialect: strict, without any of the 1.21.5 extensions.</summary>
    public static readonly SnbtOptions v1_13 = new();

    /// <summary>The modern 1.21.5 dialect: enables every 1.21.5 extension.</summary>
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
    };
}

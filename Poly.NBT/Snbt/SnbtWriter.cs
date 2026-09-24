using System.Globalization;
using System.Text;
using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

/// <summary>Writes <see cref="NbtElement"/> trees as compact, single-line SNBT text.</summary>
/// <remarks>
/// <para>
/// The <see cref="TextWriter"/> overloads stream the document: each scalar is formatted into a small stack
/// buffer and written as soon as it is produced, so the complete document is never materialized as a string.
/// The <see cref="string"/> overloads are convenience wrappers that collect the same output in a
/// <see cref="StringWriter"/>.
/// </para>
/// <para>
/// Exactly one aspect of the output depends on the dialect supplied via <see cref="SnbtOptions"/>: floating-point
/// values only use <c>E</c>-notation when <see cref="SnbtOptions.AllowScientificNotation"/> is enabled, and are
/// expanded into an equivalent plain decimal literal otherwise. Every other flag describes what the parser
/// accepts and cannot be selected by the writer, which never emits trailing commas, hexadecimal or binary
/// literals, underscores, signedness suffixes, or <c>bool()</c>/<c>uuid()</c> operations, and always writes
/// explicit type suffixes on numbers.
/// </para>
/// <para>
/// A heterogeneous <see cref="NbtList"/> cannot be represented in a dialect without
/// <see cref="SnbtOptions.AllowHeterogeneousLists"/>, because the element suffixes carry the tag types; such a
/// list is written as-is and will be rejected when parsed back with that dialect.
/// </para>
/// </remarks>
public static class SnbtWriter
{
    /// <summary>The nesting level of the outermost value.</summary>
    private const int RootDepth = 1;

    /// <summary>The bound covers the widest expansion any <see cref="double"/> can need: 324 fractional digits.</summary>
    private static readonly string Zeroes = new('0', 400);

    /// <summary>Writes an element to the given writer using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static void Write(TextWriter writer, NbtElement element)
        => Write(writer, element, SnbtOptions.v1_21_5);

    /// <summary>Writes an element to the given writer using the specified dialect.</summary>
    public static void Write(TextWriter writer, NbtElement element, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(element);
        AppendElement(writer, element, options, RootDepth);
    }

    /// <summary>Writes the root element of a document using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static void Write(TextWriter writer, NbtDocument document)
        => Write(writer, document, SnbtOptions.v1_21_5);

    /// <summary>Writes the root element of a document using the specified dialect.</summary>
    public static void Write(TextWriter writer, NbtDocument document, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(document);
        AppendElement(writer, document.RootElement, options, RootDepth);
    }

    /// <summary>Formats an element as SNBT text using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static string Write(NbtElement element)
        => Write(element, SnbtOptions.v1_21_5);

    /// <summary>Formats an element as SNBT text using the specified dialect.</summary>
    public static string Write(NbtElement element, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AppendElement(writer, element, options, RootDepth);
        return writer.ToString();
    }

    /// <summary>Formats the root element of a document using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static string Write(NbtDocument document)
        => Write(document, SnbtOptions.v1_21_5);

    /// <summary>Formats the root element of a document using the specified dialect.</summary>
    public static string Write(NbtDocument document, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Write(document.RootElement, options);
    }

    private static void AppendElement(TextWriter writer, NbtElement element, SnbtOptions options, int depth)
    {
        switch (element)
        {
            case NbtByte item: AppendInteger(writer, item.Value, 'b'); break;
            case NbtShort item: AppendInteger(writer, item.Value, 's'); break;
            case NbtInt item: AppendInteger(writer, item.Value); break;
            case NbtLong item: AppendInteger(writer, item.Value, 'L'); break;
            case NbtFloat item: AppendFloat(writer, item.Value, options); break;
            case NbtDouble item: AppendDouble(writer, item.Value, options); break;
            case NbtString item: AppendString(writer, item.Value); break;
            case NbtList item: AppendList(writer, item, options, depth); break;
            case NbtCompound item: AppendCompound(writer, item, options, depth); break;
            case NbtByteArray item: AppendByteArray(writer, item.Value); break;
            case NbtIntArray item: AppendIntArray(writer, item.Value); break;
            case NbtLongArray item: AppendLongArray(writer, item.Value); break;
            default: throw new NotSupportedException($"Unknown NBT DOM type {element.GetType()}.");
        }
    }

    /// <summary>
    /// Returns the nesting level of a child of the value at <paramref name="depth"/>, or throws when that would
    /// exceed <see cref="SnbtOptions.MaxDepth"/>.
    /// </summary>
    /// <remarks>
    /// Writing recurses once per nesting level, so an arbitrarily deep user-built tree would otherwise exhaust
    /// the stack. <see cref="StackOverflowException"/> cannot be caught in .NET, so the limit is the only way to
    /// fail recoverably.
    /// </remarks>
    private static int Descend(SnbtOptions options, int depth)
    {
        int next = depth + 1;
        return next > options.EffectiveMaxDepth
            ? throw new InvalidDataException($"The SNBT document nests more than {options.EffectiveMaxDepth} levels deep; raise SnbtOptions.MaxDepth to accept it.")
            : next;
    }

    /// <summary>Writes an integer in the invariant culture, optionally followed by a type suffix.</summary>
    private static void AppendInteger(TextWriter writer, long value, char suffix = '\0')
    {
        Span<char> buffer = stackalloc char[24];
        if (!value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture))
            throw new InvalidOperationException($"Unable to format the integer {value}.");

        writer.Write(buffer[..written]);
        if (suffix != '\0') writer.Write(suffix);
    }

    private static void AppendList(TextWriter writer, NbtList list, SnbtOptions options, int depth)
    {
        writer.Write('[');
        for (int index = 0; index < list.Count; index++)
        {
            if (index > 0) writer.Write(',');
            AppendElement(writer, list[index], options, Descend(options, depth));
        }

        writer.Write(']');
    }

    private static void AppendCompound(TextWriter writer, NbtCompound compound, SnbtOptions options, int depth)
    {
        writer.Write('{');
        bool first = true;
        foreach ((string key, NbtElement value) in compound)
        {
            if (!first) writer.Write(',');
            first = false;
            AppendString(writer, key);
            writer.Write(':');
            AppendElement(writer, value, options, Descend(options, depth));
        }

        writer.Write('}');
    }

    private static void AppendByteArray(TextWriter writer, byte[] values)
    {
        writer.Write("[B;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) writer.Write(',');
            AppendInteger(writer, unchecked((sbyte)values[index]), 'b');
        }

        writer.Write(']');
    }

    private static void AppendIntArray(TextWriter writer, int[] values)
    {
        writer.Write("[I;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) writer.Write(',');
            AppendInteger(writer, values[index]);
        }

        writer.Write(']');
    }

    private static void AppendLongArray(TextWriter writer, long[] values)
    {
        writer.Write("[L;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) writer.Write(',');
            AppendInteger(writer, values[index], 'L');
        }

        writer.Write(']');
    }

    private static void AppendFloat(TextWriter writer, float value, SnbtOptions options)
    {
        if (float.IsNaN(value)) { AppendSpecialFloat(writer, "NaN", 'f'); return; }
        if (float.IsPositiveInfinity(value)) { AppendSpecialFloat(writer, "Infinity", 'f'); return; }
        if (float.IsNegativeInfinity(value)) { AppendSpecialFloat(writer, "-Infinity", 'f'); return; }

        Span<char> buffer = stackalloc char[32];
        if (!value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture))
            throw new InvalidOperationException($"Unable to format the float {value}.");

        AppendFloatLiteral(writer, buffer[..written], options);
        writer.Write('f');
    }

    private static void AppendDouble(TextWriter writer, double value, SnbtOptions options)
    {
        if (double.IsNaN(value)) { AppendSpecialFloat(writer, "NaN", 'd'); return; }
        if (double.IsPositiveInfinity(value)) { AppendSpecialFloat(writer, "Infinity", 'd'); return; }
        if (double.IsNegativeInfinity(value)) { AppendSpecialFloat(writer, "-Infinity", 'd'); return; }

        Span<char> buffer = stackalloc char[32];
        if (!value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture))
            throw new InvalidOperationException($"Unable to format the double {value}.");

        AppendFloatLiteral(writer, buffer[..written], options);
        writer.Write('d');
    }

    private static void AppendSpecialFloat(TextWriter writer, string text, char suffix)
    {
        writer.Write(text);
        writer.Write(suffix);
    }

    /// <summary>Writes the body of a floating-point literal, followed by nothing: the caller writes the suffix.</summary>
    private static void AppendFloatLiteral(TextWriter writer, ReadOnlySpan<char> text, SnbtOptions options)
    {
        int marker = text.IndexOfAny('E', 'e');

        if (marker >= 0 && !options.AllowScientificNotation)
        {
            AppendPlainDecimal(writer, text, marker);
            return;
        }

        writer.Write(text);
        if (marker < 0 && text.IndexOf('.') < 0) writer.Write(".0");
    }

    /// <summary>
    /// Writes an <c>E</c>-notation literal as an equivalent plain decimal literal, for dialects that do not
    /// accept an exponent. The digits are moved rather than the value re-formatted, so the round-trip fidelity
    /// of the shortest round-trippable representation is preserved exactly.
    /// </summary>
    private static void AppendPlainDecimal(TextWriter writer, ReadOnlySpan<char> text, int marker)
    {
        int exponent = int.Parse(text[(marker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        ReadOnlySpan<char> mantissa = text[..marker];
        if (mantissa.StartsWith("-"))
        {
            writer.Write('-');
            mantissa = mantissa[1..];
        }

        // The "R" format yields at most 17 significant digits, so the buffer always covers the mantissa.
        Span<char> digits = stackalloc char[32];
        int digitCount = 0;
        for (int index = 0; index < mantissa.Length; index++)
        {
            if (mantissa[index] != '.') digits[digitCount++] = mantissa[index];
        }

        int point = mantissa.IndexOf('.');
        int integerLength = point < 0 ? mantissa.Length : point;
        int pointPosition = integerLength + exponent;

        if (pointPosition <= 0)
        {
            writer.Write("0.");
            WriteZeroes(writer, -pointPosition);
            writer.Write(digits[..digitCount]);
        }
        else if (pointPosition >= digitCount)
        {
            writer.Write(digits[..digitCount]);
            WriteZeroes(writer, pointPosition - digitCount);
            writer.Write(".0");
        }
        else
        {
            writer.Write(digits[..pointPosition]);
            writer.Write('.');
            writer.Write(digits[pointPosition..digitCount]);
        }
    }

    private static void WriteZeroes(TextWriter writer, int count)
    {
        while (count > 0)
        {
            int chunk = Math.Min(count, Zeroes.Length);
            writer.Write(Zeroes.AsSpan(0, chunk));
            count -= chunk;
        }
    }

    private static void AppendString(TextWriter writer, string value)
    {
        if (CanWriteBare(value))
        {
            writer.Write(value);
            return;
        }

        if (value.IndexOf('"') >= 0 && value.IndexOf('\'') < 0 && !HasControlCharacter(value))
        {
            writer.Write('\'');
            foreach (char character in value)
            {
                if (character is '\'' or '\\') writer.Write('\\');
                writer.Write(character);
            }

            writer.Write('\'');
            return;
        }

        writer.Write('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': writer.Write("\\\""); break;
                case '\\': writer.Write("\\\\"); break;
                case '\n': writer.Write("\\n"); break;
                case '\t': writer.Write("\\t"); break;
                case '\r': writer.Write("\\r"); break;
                default:
                    if (character < ' ')
                    {
                        writer.Write("\\u");
                        writer.Write(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.Write(character);
                    }

                    break;
            }
        }

        writer.Write('"');
    }

    private static bool CanWriteBare(string value)
    {
        if (value.Length == 0) return false;
        if (value[0] is >= '0' and <= '9') return false;

        foreach (char character in value)
        {
            if (!SnbtLexer.IsBareChar(character)) return false;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

        // Quoting is deliberately dialect-independent: it uses the most permissive dialect, so a value that any
        // dialect could read as a number - or reject as a malformed one - is quoted in every dialect. Quoted strings
        // are valid everywhere, which keeps the written text readable regardless of which dialect reads it back.
        return SnbtNumbers.TryParse(value, SnbtOptions.v1_21_5, out _, out _) != SnbtNumberStatus.Success;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (char character in value)
        {
            if (character < ' ') return true;
        }

        return false;
    }
}

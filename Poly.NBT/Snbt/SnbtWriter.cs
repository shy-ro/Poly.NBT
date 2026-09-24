using System.Globalization;
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
/// Numbers are written so that the text matches what Minecraft prints for the same value, which keeps
/// golden files, diffs, and checksums over SNBT output meaningful. Floating-point literals follow Java's
/// <c>Double.toString</c>/<c>Float.toString</c> shape; see <see cref="AppendFloatLiteral"/> for the exact rules.
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

            // A container can still hold a null, because the list and compound constructors copy their input
            // rather than scan it. Fail with the same error and message the binary writer uses, so a malformed
            // tree is reported the same way whichever format it is written to.
            case null: throw new InvalidDataException("NBT DOM values cannot be null.");
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

    /// <summary>Writes the body of a floating-point literal. The caller writes the type suffix afterwards.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="text">The runtime's shortest round-trippable (<c>"R"</c>) representation of the value.</param>
    /// <param name="options">The dialect, which decides whether an exponent may be used at all.</param>
    /// <remarks>
    /// <para>
    /// The digits come from the <c>"R"</c> format but the shape follows Java's
    /// <c>Double.toString</c>/<c>Float.toString</c>, which is what Minecraft itself prints:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The mantissa always keeps a decimal point and at least one digit after it: <c>1.0E20</c>, not <c>1E+20</c>.</description></item>
    /// <item><description>The exponent carries no <c>+</c> and no leading zeros: <c>1.2345678901234568E17</c>, not <c>1.2345678901234568E+17</c>.</description></item>
    /// <item><description><c>E</c>-notation is used outside <c>[10^-3, 10^7)</c>; the <c>"R"</c> format stays plain until <c>10^15</c>.</description></item>
    /// </list>
    /// <para>
    /// Divergent text for the same value is not a parse error - Java reads both forms - but it turns up as a
    /// stable false positive in any text diff, checksum, or cache key computed over SNBT output, so the shapes
    /// are aligned. Digit selection can still differ from Java for a handful of extreme subnormals such as
    /// <c>double.Epsilon</c>, where the two runtimes pick different shortest representations of the same value;
    /// both parse back to that value.
    /// </para>
    /// <para>
    /// When <see cref="SnbtOptions.AllowScientificNotation"/> is off, an exponent is not an option and the
    /// digits are moved into an equivalent plain decimal instead. Nothing is re-rounded: the placement is
    /// arithmetic on the shortest representation, so the round-trip guarantee survives.
    /// </para>
    /// </remarks>
    private static void AppendFloatLiteral(TextWriter writer, ReadOnlySpan<char> text, SnbtOptions options)
    {
        bool negative = text.Length > 0 && text[0] == '-';
        ReadOnlySpan<char> mantissa = negative ? text[1..] : text;
        int exponent = 0;

        int marker = mantissa.IndexOfAny('E', 'e');
        if (marker >= 0)
        {
            exponent = int.Parse(mantissa[(marker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            mantissa = mantissa[..marker];
        }

        // Reduce the literal to significant digits plus the position of its decimal point. "100", "1E2" and
        // "1.00E+2" all describe the same number and have to converge here, because the target notation is
        // chosen from the value rather than from the input shape. The "R" format yields at most 17 significant
        // digits, so the buffer always covers the mantissa.
        Span<char> digits = stackalloc char[32];
        int digitCount = 0;
        int integerDigits = 0;
        bool seenPoint = false;
        foreach (char character in mantissa)
        {
            if (character == '.')
            {
                seenPoint = true;
                continue;
            }

            digits[digitCount++] = character;
            if (!seenPoint) integerDigits++;
        }

        int point = integerDigits + exponent;
        int first = 0;
        while (first < digitCount && digits[first] == '0') first++;
        int last = digitCount;
        while (last > first && digits[last - 1] == '0') last--;

        if (negative) writer.Write('-');

        if (first == digitCount)
        {
            // Every digit was a zero, so the value is zero and the point position carries no information.
            writer.Write("0.0");
            return;
        }

        point -= first;
        ReadOnlySpan<char> significant = digits[first..last];
        int magnitude = point - 1; // the value is d.ddd x 10^magnitude

        if (options.AllowScientificNotation && (magnitude < -3 || magnitude > 6))
        {
            writer.Write(significant[0]);
            writer.Write('.');
            if (significant.Length > 1) writer.Write(significant[1..]);
            else writer.Write('0');

            writer.Write('E');
            writer.Write(magnitude.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (point <= 0)
        {
            writer.Write("0.");
            WriteZeroes(writer, -point);
            writer.Write(significant);
        }
        else if (point >= significant.Length)
        {
            writer.Write(significant);
            WriteZeroes(writer, point - significant.Length);
            writer.Write(".0");
        }
        else
        {
            writer.Write(significant[..point]);
            writer.Write('.');
            writer.Write(significant[point..]);
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
        if (value is null) throw new InvalidDataException("NBT DOM values cannot be null.");

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

        // SNBT reserves a leading sign or point for numbers: Minecraft's tokenizer tries a numeric parse first
        // and reports an error instead of falling back to a bare string, so `{a:-foo}` is not valid SNBT even
        // though this library's own parser recovers. `+` is included because the reader accepts `+1`.
        if (value[0] is (>= '0' and <= '9') or '-' or '+' or '.') return false;

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

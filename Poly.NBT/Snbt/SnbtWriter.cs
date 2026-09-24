using System.Globalization;
using System.Text;
using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

/// <summary>Writes <see cref="NbtElement"/> trees as compact, single-line SNBT text.</summary>
/// <remarks>
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
    /// <summary>Writes an element to the given writer using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static void Write(TextWriter writer, NbtElement element)
        => Write(writer, element, SnbtOptions.v1_21_5);

    /// <summary>Writes an element to the given writer using the specified dialect.</summary>
    public static void Write(TextWriter writer, NbtElement element, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Write(element, options));
    }

    /// <summary>Writes the root element of a document using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static void Write(TextWriter writer, NbtDocument document)
        => Write(writer, document, SnbtOptions.v1_21_5);

    /// <summary>Writes the root element of a document using the specified dialect.</summary>
    public static void Write(TextWriter writer, NbtDocument document, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Write(document, options));
    }

    /// <summary>Formats an element as SNBT text using the <see cref="SnbtOptions.v1_21_5"/> dialect.</summary>
    public static string Write(NbtElement element)
        => Write(element, SnbtOptions.v1_21_5);

    /// <summary>Formats an element as SNBT text using the specified dialect.</summary>
    public static string Write(NbtElement element, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(element);
        var builder = new StringBuilder();
        Append(builder, element, options);
        return builder.ToString();
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

    private static void Append(StringBuilder builder, NbtElement element, SnbtOptions options)
    {
        switch (element)
        {
            case NbtByte item: AppendInteger(builder, item.Value, "b"); break;
            case NbtShort item: AppendInteger(builder, item.Value, "s"); break;
            case NbtInt item: builder.Append(item.Value.ToString(CultureInfo.InvariantCulture)); break;
            case NbtLong item: AppendInteger(builder, item.Value, "L"); break;
            case NbtFloat item: builder.Append(FormatFloat(item.Value, options)).Append('f'); break;
            case NbtDouble item: builder.Append(FormatDouble(item.Value, options)).Append('d'); break;
            case NbtString item: builder.Append(FormatString(item.Value)); break;
            case NbtList item: AppendList(builder, item, options); break;
            case NbtCompound item: AppendCompound(builder, item, options); break;
            case NbtByteArray item: AppendByteArray(builder, item.Value); break;
            case NbtIntArray item: AppendIntArray(builder, item.Value); break;
            case NbtLongArray item: AppendLongArray(builder, item.Value); break;
            default: throw new NotSupportedException($"Unknown NBT DOM type {element.GetType()}.");
        }
    }

    private static void AppendInteger(StringBuilder builder, long value, string suffix)
        => builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(suffix);

    private static void AppendList(StringBuilder builder, NbtList list, SnbtOptions options)
    {
        builder.Append('[');
        for (int index = 0; index < list.Count; index++)
        {
            if (index > 0) builder.Append(',');
            Append(builder, list[index], options);
        }

        builder.Append(']');
    }

    private static void AppendCompound(StringBuilder builder, NbtCompound compound, SnbtOptions options)
    {
        builder.Append('{');
        bool first = true;
        foreach ((string key, NbtElement value) in compound)
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(FormatString(key)).Append(':');
            Append(builder, value, options);
        }

        builder.Append('}');
    }

    private static void AppendByteArray(StringBuilder builder, byte[] values)
    {
        builder.Append("[B;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) builder.Append(',');
            AppendInteger(builder, unchecked((sbyte)values[index]), "b");
        }

        builder.Append(']');
    }

    private static void AppendIntArray(StringBuilder builder, int[] values)
    {
        builder.Append("[I;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append(values[index].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    private static void AppendLongArray(StringBuilder builder, long[] values)
    {
        builder.Append("[L;");
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0) builder.Append(',');
            AppendInteger(builder, values[index], "L");
        }

        builder.Append(']');
    }

    private static string FormatFloat(float value, SnbtOptions options)
    {
        if (float.IsNaN(value)) return "NaN";
        if (float.IsPositiveInfinity(value)) return "Infinity";
        if (float.IsNegativeInfinity(value)) return "-Infinity";

        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!options.AllowScientificNotation) text = ExpandExponent(text);
        return NeedsDecimalPoint(text) ? text + ".0" : text;
    }

    private static string FormatDouble(double value, SnbtOptions options)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!options.AllowScientificNotation) text = ExpandExponent(text);
        return NeedsDecimalPoint(text) ? text + ".0" : text;
    }

    /// <summary>
    /// Rewrites an <c>E</c>-notation literal as an equivalent plain decimal literal, for dialects that do not
    /// accept an exponent. The digits are moved rather than the value re-formatted, so the round-trip fidelity
    /// of the shortest round-trippable representation is preserved exactly.
    /// </summary>
    private static string ExpandExponent(string text)
    {
        int marker = text.IndexOfAny(['E', 'e']);
        if (marker < 0) return text;

        int exponent = int.Parse(text[(marker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        string mantissa = text[..marker];
        bool negative = mantissa.StartsWith('-');
        if (negative) mantissa = mantissa[1..];

        int point = mantissa.IndexOf('.');
        string digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
        int integerLength = point < 0 ? mantissa.Length : point;
        int pointPosition = integerLength + exponent;

        var builder = new StringBuilder(digits.Length + Math.Abs(exponent) + 3);
        if (negative) builder.Append('-');

        if (pointPosition <= 0)
        {
            builder.Append("0.").Append('0', -pointPosition).Append(digits);
        }
        else if (pointPosition >= digits.Length)
        {
            builder.Append(digits).Append('0', pointPosition - digits.Length);
        }
        else
        {
            builder.Append(digits, 0, pointPosition).Append('.').Append(digits, pointPosition, digits.Length - pointPosition);
        }

        return builder.ToString();
    }

    private static bool NeedsDecimalPoint(string text)
        => text.IndexOf('.') < 0 && text.IndexOf('E') < 0 && text.IndexOf('e') < 0;

    private static string FormatString(string value)
    {
        if (CanWriteBare(value)) return value;
        if (value.IndexOf('"') >= 0 && value.IndexOf('\'') < 0 && !HasControlCharacter(value)) return FormatSingleQuoted(value);
        return FormatDoubleQuoted(value);
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

    private static string FormatSingleQuoted(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('\'');
        foreach (char character in value)
        {
            if (character is '\'' or '\\') builder.Append('\\');
            builder.Append(character);
        }

        return builder.Append('\'').ToString();
    }

    private static string FormatDoubleQuoted(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                case '\r': builder.Append("\\r"); break;
                default:
                    if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else builder.Append(character);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}

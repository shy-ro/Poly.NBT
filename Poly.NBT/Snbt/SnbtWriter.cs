using System.Globalization;
using System.Text;
using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

/// <summary>Writes <see cref="NbtElement"/> trees as compact, single-line SNBT text.</summary>
public static class SnbtWriter
{
    /// <summary>Writes an element to the given writer.</summary>
    public static void Write(TextWriter writer, NbtElement element)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Write(element));
    }

    /// <summary>Writes the root element of a document to the given writer.</summary>
    public static void Write(TextWriter writer, NbtDocument document)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Write(document));
    }

    /// <summary>Formats an element as SNBT text.</summary>
    public static string Write(NbtElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var builder = new StringBuilder();
        Append(builder, element);
        return builder.ToString();
    }

    /// <summary>Formats the root element of a document as SNBT text.</summary>
    public static string Write(NbtDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Write(document.RootElement);
    }

    private static void Append(StringBuilder builder, NbtElement element)
    {
        switch (element)
        {
            case NbtByte item: AppendInteger(builder, item.Value, "b"); break;
            case NbtShort item: AppendInteger(builder, item.Value, "s"); break;
            case NbtInt item: builder.Append(item.Value.ToString(CultureInfo.InvariantCulture)); break;
            case NbtLong item: AppendInteger(builder, item.Value, "L"); break;
            case NbtFloat item: builder.Append(FormatSingle(item.Value)).Append('f'); break;
            case NbtDouble item: builder.Append(FormatDouble(item.Value)).Append('d'); break;
            case NbtString item: builder.Append(FormatString(item.Value)); break;
            case NbtList item: AppendList(builder, item); break;
            case NbtCompound item: AppendCompound(builder, item); break;
            case NbtByteArray item: AppendByteArray(builder, item.Value); break;
            case NbtIntArray item: AppendIntArray(builder, item.Value); break;
            case NbtLongArray item: AppendLongArray(builder, item.Value); break;
            default: throw new NotSupportedException($"Unknown NBT DOM type {element.GetType()}.");
        }
    }

    private static void AppendInteger(StringBuilder builder, long value, string suffix)
        => builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(suffix);

    private static void AppendList(StringBuilder builder, NbtList list)
    {
        builder.Append('[');
        for (int index = 0; index < list.Count; index++)
        {
            if (index > 0) builder.Append(',');
            Append(builder, list[index]);
        }

        builder.Append(']');
    }

    private static void AppendCompound(StringBuilder builder, NbtCompound compound)
    {
        builder.Append('{');
        bool first = true;
        foreach ((string key, NbtElement value) in compound)
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(FormatString(key)).Append(':');
            Append(builder, value);
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

    private static string FormatSingle(float value)
    {
        if (float.IsNaN(value)) return "NaN";
        if (float.IsPositiveInfinity(value)) return "Infinity";
        if (float.IsNegativeInfinity(value)) return "-Infinity";

        string text = value.ToString("R", CultureInfo.InvariantCulture);
        return NeedsDecimalPoint(text) ? text + ".0" : text;
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        string text = value.ToString("R", CultureInfo.InvariantCulture);
        return NeedsDecimalPoint(text) ? text + ".0" : text;
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

        // Anything the modern dialect would read as a number must be quoted to survive a round trip.
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

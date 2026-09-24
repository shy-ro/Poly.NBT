using System.Text;

namespace Poly.NBT.Snbt;

/// <summary>Character-level scanner over an in-memory SNBT document.</summary>
/// <remarks>
/// This is a <see langword="ref struct"/> so that it can hold the input as a <see cref="ReadOnlySpan{T}"/>
/// rather than a string. Because a struct copy would not share the cursor, every parser method that reads
/// from it takes it by reference.
/// </remarks>
internal ref struct SnbtLexer
{
    private readonly ReadOnlySpan<char> _text;
    private int _position;

    public SnbtLexer(ReadOnlySpan<char> text)
    {
        _text = text;
        _position = 0;
    }

    public int Position => _position;

    public bool AtEnd => _position >= _text.Length;

    public char Current => _position < _text.Length ? _text[_position] : '\0';

    public char Peek(int ahead = 0)
        => _position + ahead < _text.Length ? _text[_position + ahead] : '\0';

    public char Advance() => _position < _text.Length ? _text[_position++] : '\0';

    public SnbtParseException Error(string message) => new(message, _position);

    public SnbtParseException ErrorAt(string message, int offset) => new(message, offset);

    public void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) _position++;
    }

    public static bool IsBareChar(char value) => value
        is (>= '0' and <= '9')
        or (>= 'A' and <= 'Z')
        or (>= 'a' and <= 'z')
        or '_' or '-' or '+' or '.';

    /// <summary>Reads a run of bare-string characters, returning a slice of the input.</summary>
    public ReadOnlySpan<char> ReadBareToken()
    {
        int start = _position;
        while (_position < _text.Length && IsBareChar(_text[_position])) _position++;
        return _text[start.._position];
    }

    /// <summary>Reads a quoted string starting at the current character, decoding escape sequences.</summary>
    /// <remarks>
    /// Both quote styles accept the same twelve escape sequences, which is what the SNBT grammar specifies:
    /// <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c>, <c>\s</c>, <c>\t</c>, <c>\\</c>, <c>\'</c>, <c>\"</c>,
    /// <c>\xhh</c>, <c>\uhhhh</c>, and <c>\UHHHHHHHH</c>. <c>\N{name}</c>, the thirteenth, is rejected.
    /// </remarks>
    public string ReadQuotedString()
    {
        int start = _position;
        char quote = Advance();
        int contentStart = _position;

        // The overwhelming majority of quoted strings contain no escape, so look for the closing quote first
        // and hand back the slice of the input directly. Only a string that really holds a backslash pays for
        // a StringBuilder and a per-character copy.
        while (!AtEnd && _text[_position] != quote && _text[_position] != '\\') _position++;

        if (AtEnd) throw ErrorAt("Unterminated string literal.", start);

        if (_text[_position] == quote)
        {
            string plain = _text.Slice(contentStart, _position - contentStart).ToString();
            _position++;
            return plain;
        }

        var builder = new StringBuilder();
        builder.Append(_text.Slice(contentStart, _position - contentStart));

        while (true)
        {
            if (AtEnd) throw ErrorAt("Unterminated string literal.", start);

            char current = Advance();
            if (current == quote) return builder.ToString();
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (AtEnd) throw ErrorAt("Unterminated escape sequence.", _position);

            int escapeOffset = _position - 1;
            char escape = Advance();
            switch (escape)
            {
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 's': builder.Append(' '); break;
                case 't': builder.Append('\t'); break;
                case '\\': builder.Append('\\'); break;
                case '\'': builder.Append('\''); break;
                case '"': builder.Append('"'); break;
                case 'x': builder.Append((char)ReadHexEscape(2, escapeOffset, "\\x")); break;
                case 'u': builder.Append((char)ReadHexEscape(4, escapeOffset, "\\u")); break;
                case 'U': builder.Append(ReadCodePointEscape(escapeOffset)); break;
                case 'N':
                    // The grammar's thirteenth escape indexes a Unicode name table. Half a table would be
                    // worse than none: it would accept \N{snowman} and reject \N{SNOWMAN} with no way for a
                    // caller to tell an unsupported name from a misspelled one, so it is refused by name.
                    throw ErrorAt("The \\N{name} escape sequence is not supported.", escapeOffset);
                default:
                    throw ErrorAt($"Unsupported escape sequence '\\{escape}'.", escapeOffset);
            }
        }
    }

    /// <summary>Reads an eight-digit <c>\U</c> escape and turns it into one or two UTF-16 code units.</summary>
    private string ReadCodePointEscape(int escapeOffset)
    {
        long codePoint = ReadHexEscape(8, escapeOffset, "\\U");
        if (codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
        {
            throw ErrorAt(
                $"A \\U escape sequence requires a Unicode code point, but U+{codePoint:X8} is not one.",
                escapeOffset);
        }

        return char.ConvertFromUtf32((int)codePoint);
    }

    /// <summary>Reads a fixed number of hexadecimal digits as an unsigned value.</summary>
    private long ReadHexEscape(int digits, int escapeOffset, string sequence)
    {
        if (_position + digits > _text.Length) throw ErrorAt($"Incomplete {sequence} escape sequence.", escapeOffset);

        long value = 0;
        for (int index = 0; index < digits; index++)
        {
            int digit = HexValue(_text[_position + index]);
            if (digit < 0)
            {
                throw ErrorAt($"{sequence} requires {digits} hexadecimal digits.", escapeOffset);
            }

            value = (value * 16) + digit;
        }

        _position += digits;
        return value;
    }

    internal static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1,
    };
}

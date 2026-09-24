using System.Text;

namespace Poly.NBT.Snbt;

/// <summary>Character-level scanner over an in-memory SNBT document.</summary>
internal sealed class SnbtLexer(string text)
{
    private readonly string _text = text;
    private int _position;

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

    /// <summary>Reads a run of bare-string characters, returning the raw text.</summary>
    public string ReadBareToken()
    {
        int start = _position;
        while (_position < _text.Length && IsBareChar(_text[_position])) _position++;
        return _text[start.._position];
    }

    /// <summary>Reads a quoted string starting at the current character, decoding escape sequences.</summary>
    public string ReadQuotedString()
    {
        int start = _position;
        char quote = Advance();
        var builder = new StringBuilder();

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
            if (quote == '\'')
            {
                // Single-quoted strings only recognize \' and \\.
                switch (escape)
                {
                    case '\'': builder.Append('\''); break;
                    case '\\': builder.Append('\\'); break;
                    default: throw ErrorAt($"Unsupported escape sequence '\\{escape}' in a single-quoted string.", escapeOffset);
                }

                continue;
            }

            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                case 'u':
                    builder.Append(ReadUnicodeEscape(escapeOffset));
                    break;
                default:
                    throw ErrorAt($"Unsupported escape sequence '\\{escape}'.", escapeOffset);
            }
        }
    }

    private char ReadUnicodeEscape(int escapeOffset)
    {
        if (_position + 4 > _text.Length) throw ErrorAt("Incomplete \\u escape sequence.", escapeOffset);

        int value = 0;
        for (int index = 0; index < 4; index++)
        {
            int digit = HexValue(_text[_position + index]);
            if (digit < 0) throw ErrorAt("A \\u escape sequence requires four hexadecimal digits.", escapeOffset);
            value = value * 16 + digit;
        }

        _position += 4;
        return (char)value;
    }

    internal static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1,
    };
}

using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

/// <summary>Reads SNBT text into <see cref="NbtElement"/> trees.</summary>
/// <remarks>
/// The text overloads take a <see cref="ReadOnlySpan{T}"/>. A <see cref="string"/> argument binds to them
/// through the built-in implicit conversion, so callers can keep passing strings directly.
/// </remarks>
public static partial class SnbtParser
{
    /// <summary>The nesting level of the outermost value.</summary>
    private const int RootDepth = 1;

    /// <summary>Parses a single SNBT value read from a text reader using the modern (<see cref="SnbtOptions.v1_21_5"/>) dialect.</summary>
    public static NbtElement Parse(TextReader reader) => Parse(reader, SnbtOptions.v1_21_5);

    /// <summary>Parses a single SNBT value read from a text reader using the given dialect.</summary>
    /// <remarks>
    /// This overload buffers the whole reader into a string before parsing, because the lexer scans a
    /// <see cref="ReadOnlySpan{T}"/>. For a large document the <see cref="ReadOnlySpan{T}"/> overload avoids that
    /// copy when the text is already in memory.
    /// </remarks>
    public static NbtElement Parse(TextReader reader, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Parse(ReadAll(reader).AsSpan(), options);
    }

    /// <summary>Parses a single SNBT value using the modern dialect.</summary>
    public static NbtElement Parse(ReadOnlySpan<char> text) => Parse(text, SnbtOptions.v1_21_5);

    /// <summary>Parses a single SNBT value using the given dialect.</summary>
    public static NbtElement Parse(ReadOnlySpan<char> text, SnbtOptions options)
    {
        var lexer = new SnbtLexer(text);
        lexer.SkipWhitespace();
        if (lexer.AtEnd) throw lexer.Error("Expected an SNBT value but reached the end of the input.");

        NbtElement element = ReadValue(ref lexer, options, RootDepth);
        lexer.SkipWhitespace();
        if (!lexer.AtEnd) throw lexer.Error("Unexpected content after the SNBT value.");
        return element;
    }

    /// <summary>
    /// Parses a single SNBT value into a document. SNBT carries no root tag name, so the document name is empty.
    /// </summary>
    public static NbtDocument ParseDocument(TextReader reader) => ParseDocument(reader, SnbtOptions.v1_21_5);

    /// <summary>Parses a single SNBT value read from a text reader into a document using the given dialect.</summary>
    /// <inheritdoc cref="Parse(TextReader, SnbtOptions)" path="/remarks"/>
    public static NbtDocument ParseDocument(TextReader reader, SnbtOptions options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ParseDocument(ReadAll(reader).AsSpan(), options);
    }

    /// <summary>Parses a single SNBT value into a document using the modern dialect.</summary>
    public static NbtDocument ParseDocument(ReadOnlySpan<char> text) => ParseDocument(text, SnbtOptions.v1_21_5);

    /// <summary>Parses a single SNBT value into a document using the given dialect.</summary>
    public static NbtDocument ParseDocument(ReadOnlySpan<char> text, SnbtOptions options)
        => new(string.Empty, Parse(text, options));

    private static string ReadAll(TextReader reader)
    {
        string? text = reader.ReadToEnd();
        return text ?? string.Empty;
    }

    /// <summary>
    /// Returns the nesting level of a child of the value at <paramref name="depth"/>, or throws when that would
    /// exceed <see cref="SnbtOptions.MaxDepth"/>.
    /// </summary>
    private static int Descend(ref SnbtLexer lexer, SnbtOptions options, int depth)
    {
        int next = depth + 1;
        return next > options.EffectiveMaxDepth
            ? throw lexer.Error($"The SNBT value nests more than {options.EffectiveMaxDepth} levels deep; raise SnbtOptions.MaxDepth to accept it.")
            : next;
    }

    private static NbtElement ReadValue(ref SnbtLexer lexer, SnbtOptions options, int depth)
    {
        lexer.SkipWhitespace();
        if (lexer.AtEnd) throw lexer.Error("Expected an SNBT value but reached the end of the input.");

        return lexer.Current switch
        {
            '{' => ReadCompound(ref lexer, options, depth),
            '[' => ReadListOrArray(ref lexer, options, depth),
            '"' or '\'' => new NbtString(lexer.ReadQuotedString()),
            _ => ReadBare(ref lexer, options, depth),
        };
    }

    private static NbtElement ReadBare(ref SnbtLexer lexer, SnbtOptions options, int depth)
    {
        int start = lexer.Position;
        ReadOnlySpan<char> token = lexer.ReadBareToken();
        if (token.Length == 0) throw lexer.Error("Expected an SNBT value.");

        if (options.AllowSnbtOperations && IsOperationName(token))
        {
            lexer.SkipWhitespace();
            if (lexer.Current == '(') return ReadOperation(ref lexer, options, token, start, depth);
        }

        if (options.AllowBooleanLiterals)
        {
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase)) return new NbtByte(1);
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase)) return new NbtByte(0);
        }

        SnbtNumberStatus status = SnbtNumbers.TryParse(token, options, out NbtElement? number, out string? error);
        if (status == SnbtNumberStatus.Success) return number!;
        if (status == SnbtNumberStatus.Invalid) throw new SnbtParseException(error!, start);

        // Only a value that is neither an operation, a boolean, nor a number becomes a string.
        return new NbtString(token.ToString());
    }

    private static bool IsOperationName(ReadOnlySpan<char> token)
        => token.Equals("bool", StringComparison.OrdinalIgnoreCase)
            || token.Equals("uuid", StringComparison.OrdinalIgnoreCase);

    private static NbtElement ReadOperation(ref SnbtLexer lexer, SnbtOptions options, ReadOnlySpan<char> name, int start, int depth)
    {
        lexer.Advance();

        if (name.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            lexer.SkipWhitespace();
            if (lexer.Current is not ('"' or '\'')) throw lexer.Error("The uuid(...) operation requires a string argument.");
            string text = lexer.ReadQuotedString();
            lexer.SkipWhitespace();
            if (lexer.Current != ')') throw lexer.Error("Expected ')' to close the uuid(...) operation.");
            lexer.Advance();
            return ParseUuid(text, start);
        }

        // An operation is not a container level of its own, so its argument is read at the same depth.
        NbtElement argument = ReadValue(ref lexer, options, depth);
        lexer.SkipWhitespace();
        if (lexer.Current != ')') throw lexer.Error("Expected ')' to close the bool(...) operation.");
        lexer.Advance();

        return argument switch
        {
            NbtByte item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            NbtShort item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            NbtInt item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            NbtLong item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            NbtFloat item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            NbtDouble item => new NbtByte(item.Value != 0 ? (sbyte)1 : (sbyte)0),
            _ => throw new SnbtParseException("The bool(...) operation accepts only numbers or booleans.", start),
        };
    }

    private static NbtIntArray ParseUuid(string text, int offset)
    {
        Span<byte> bytes = stackalloc byte[16];
        int nibbles = 0;
        foreach (char character in text)
        {
            if (character == '-') continue;
            int value = SnbtLexer.HexValue(character);
            if (value < 0 || nibbles >= 32) throw new SnbtParseException($"'{text}' is not a valid UUID.", offset);
            if ((nibbles & 1) == 0) bytes[nibbles >> 1] = (byte)(value << 4);
            else bytes[nibbles >> 1] |= (byte)value;
            nibbles++;
        }

        if (nibbles != 32) throw new SnbtParseException($"'{text}' is not a valid UUID.", offset);

        int[] values = new int[4];
        for (int index = 0; index < 4; index++)
        {
            values[index] = (bytes[index * 4] << 24)
                | (bytes[index * 4 + 1] << 16)
                | (bytes[index * 4 + 2] << 8)
                | bytes[index * 4 + 3];
        }

        return new NbtIntArray(values);
    }
}

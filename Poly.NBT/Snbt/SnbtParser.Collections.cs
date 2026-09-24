using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

public static partial class SnbtParser
{
    private static NbtCompound ReadCompound(SnbtLexer lexer, SnbtOptions options)
    {
        lexer.Advance();
        var entries = new List<KeyValuePair<string, NbtElement>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        lexer.SkipWhitespace();
        if (lexer.Current == '}')
        {
            lexer.Advance();
            return new NbtCompound(entries);
        }

        while (true)
        {
            lexer.SkipWhitespace();
            int keyOffset = lexer.Position;
            string key = ReadKey(lexer);

            lexer.SkipWhitespace();
            if (lexer.Current != ':') throw lexer.Error("Expected ':' after a compound key.");
            lexer.Advance();

            NbtElement value = ReadValue(lexer, options);
            if (!seen.Add(key)) throw new SnbtParseException($"Duplicate compound key '{key}'.", keyOffset);
            entries.Add(new(key, value));

            if (EndOfElements(lexer, options, '}')) return new NbtCompound(entries);
        }
    }

    private static string ReadKey(SnbtLexer lexer)
    {
        lexer.SkipWhitespace();
        if (lexer.AtEnd) throw lexer.Error("Expected a compound key.");
        if (lexer.Current is '"' or '\'') return lexer.ReadQuotedString();

        string token = lexer.ReadBareToken();
        return token.Length == 0 ? throw lexer.Error("Expected a compound key.") : token;
    }

    private static NbtElement ReadListOrArray(SnbtLexer lexer, SnbtOptions options)
    {
        lexer.Advance();
        lexer.SkipWhitespace();

        char marker = lexer.Current;
        if (marker is 'B' or 'I' or 'L')
        {
            int offset = 1;
            while (char.IsWhiteSpace(lexer.Peek(offset))) offset++;
            if (lexer.Peek(offset) == ';')
            {
                lexer.Advance();
                lexer.SkipWhitespace();
                lexer.Advance();
                return ReadArray(lexer, options, marker);
            }
        }

        return ReadList(lexer, options);
    }

    private static NbtList ReadList(SnbtLexer lexer, SnbtOptions options)
    {
        var elements = new List<NbtElement>();
        NbtElement? first = null;

        lexer.SkipWhitespace();
        if (lexer.Current == ']')
        {
            lexer.Advance();
            return new NbtList(elements);
        }

        while (true)
        {
            lexer.SkipWhitespace();
            int elementOffset = lexer.Position;
            NbtElement element = ReadValue(lexer, options);
            if (first is null) first = element;
            else if (!options.AllowHeterogeneousLists && element.GetType() != first.GetType())
                throw new SnbtParseException("Heterogeneous lists are not allowed in this dialect.", elementOffset);

            elements.Add(element);
            if (EndOfElements(lexer, options, ']')) return new NbtList(elements);
        }
    }

    private static NbtElement ReadArray(SnbtLexer lexer, SnbtOptions options, char marker)
    {
        var values = new List<long>();

        lexer.SkipWhitespace();
        if (lexer.Current == ']')
        {
            lexer.Advance();
            return BuildArray(marker, values);
        }

        while (true)
        {
            lexer.SkipWhitespace();
            int elementOffset = lexer.Position;
            values.Add(ReadArrayElement(lexer, options, marker, elementOffset));
            if (EndOfElements(lexer, options, ']')) return BuildArray(marker, values);
        }
    }

    private static long ReadArrayElement(SnbtLexer lexer, SnbtOptions options, char marker, int offset)
    {
        long value = ReadValue(lexer, options) switch
        {
            NbtByte item => item.Value,
            NbtShort item => item.Value,
            NbtInt item => item.Value,
            NbtLong item => item.Value,
            _ => throw new SnbtParseException("NBT array elements must be integers.", offset),
        };

        long minimum = marker switch { 'B' => sbyte.MinValue, 'I' => int.MinValue, _ => long.MinValue };
        long maximum = marker switch { 'B' => sbyte.MaxValue, 'I' => int.MaxValue, _ => long.MaxValue };
        if (value < minimum || value > maximum)
            throw new SnbtParseException($"The value {value} is out of range for a '{marker}' array element.", offset);

        return value;
    }

    private static NbtElement BuildArray(char marker, List<long> values)
    {
        switch (marker)
        {
            case 'B':
                byte[] bytes = new byte[values.Count];
                for (int index = 0; index < bytes.Length; index++) bytes[index] = unchecked((byte)(sbyte)values[index]);
                return new NbtByteArray(bytes);
            case 'I':
                int[] integers = new int[values.Count];
                for (int index = 0; index < integers.Length; index++) integers[index] = (int)values[index];
                return new NbtIntArray(integers);
            default:
                return new NbtLongArray([.. values]);
        }
    }

    /// <summary>Consumes the separator that follows a collection entry and reports whether the collection ended.</summary>
    private static bool EndOfElements(SnbtLexer lexer, SnbtOptions options, char terminator)
    {
        lexer.SkipWhitespace();
        if (lexer.Current == ',')
        {
            lexer.Advance();
            lexer.SkipWhitespace();
            if (lexer.Current != terminator) return false;
            if (!options.AllowTrailingCommas) throw lexer.Error("Trailing commas are not allowed in this dialect.");
        }
        else if (lexer.Current != terminator)
        {
            throw lexer.Error($"Expected ',' or '{terminator}' in a collection.");
        }

        lexer.Advance();
        return true;
    }
}

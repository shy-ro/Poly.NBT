using System.Text;

namespace Poly.NBT.Internal;

internal sealed class NbtStringCodec(NbtStringEncoding encoding, NbtLengthCodec lengths)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string Read(Stream stream)
    {
        byte[] bytes = StreamIO.ReadExactly(stream, lengths.ReadStringLength(stream));
        return encoding switch
        {
            NbtStringEncoding.ModifiedUtf8 => ModifiedUtf8.Decode(bytes),
            NbtStringEncoding.Utf8 => StrictUtf8.GetString(bytes),
            NbtStringEncoding.Utf8WithEscapes => Utf8WithEscapes.Decode(bytes),
            _ => throw new InvalidOperationException(),
        };
    }

    public void Write(Stream stream, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = encoding switch
        {
            NbtStringEncoding.ModifiedUtf8 => ModifiedUtf8.Encode(value),
            NbtStringEncoding.Utf8 => StrictUtf8.GetBytes(value),
            NbtStringEncoding.Utf8WithEscapes => Utf8WithEscapes.Encode(value),
            _ => throw new InvalidOperationException(),
        };

        lengths.WriteStringLength(stream, bytes.Length);
        stream.Write(bytes);
    }
}

internal static class Utf8WithEscapes
{
    public static byte[] Encode(string value)
    {
        using var output = new MemoryStream();
        Span<byte> encoded = stackalloc byte[4];
        for (int i = 0; i < value.Length;)
        {
            char character = value[i];
            if (character is >= '\udc80' and <= '\udcff')
            {
                byte raw = (byte)(character - '\udc00');
                output.WriteByte(0x1b);
                output.WriteByte((byte)'x');
                output.WriteByte(Hex(raw >> 4));
                output.WriteByte(Hex(raw & 0xf));
                i++;
                continue;
            }

            System.Buffers.OperationStatus status = Rune.DecodeFromUtf16(value.AsSpan(i), out Rune rune, out int consumed);
            if (status != System.Buffers.OperationStatus.Done) throw new EncoderFallbackException("The string contains an unpaired UTF-16 surrogate.");
            int count = rune.EncodeToUtf8(encoded);
            output.Write(encoded[..count]);
            i += consumed;
        }
        return output.ToArray();

        static byte Hex(int value) => (byte)(value < 10 ? '0' + value : 'A' + value - 10);
    }

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var buffer = new byte[bytes.Length];
        int offset = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0x1b && i + 3 < bytes.Length && (bytes[i + 1] is (byte)'x' or (byte)'X') &&
                Hex(bytes[i + 2]) >= 0 && Hex(bytes[i + 3]) >= 0)
            {
                buffer[offset++] = (byte)(Hex(bytes[i + 2]) * 16 + Hex(bytes[i + 3]));
                i += 3;
            }
            else buffer[offset++] = bytes[i];
        }
        // Amulet-NBT's utf8_escape codec follows Python's surrogateescape convention for
        // undecodable bytes and represents them on the wire as ESC x HH. Hex digits are
        // accepted case-insensitively; incomplete/non-x ESC sequences remain literal.
        // https://github.com/Amulet-Team/Amulet-NBT
        var result = new StringBuilder(offset);
        ReadOnlySpan<byte> source = buffer.AsSpan(0, offset);
        while (!source.IsEmpty)
        {
            System.Buffers.OperationStatus status = Rune.DecodeFromUtf8(source, out Rune rune, out int consumed);
            if (status == System.Buffers.OperationStatus.Done)
            {
                result.Append(rune);
                source = source[consumed..];
            }
            else
            {
                result.Append((char)(0xdc00 + source[0]));
                source = source[1..];
            }
        }
        return result.ToString();

        static int Hex(byte value) => value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
            >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
            _ => -1,
        };
    }
}

internal static class ModifiedUtf8
{
    public static byte[] Encode(string value)
    {
        int length = 0;
        foreach (char character in value)
        {
            length += character is >= '\u0001' and <= '\u007f' ? 1 : character <= '\u07ff' ? 2 : 3;
        }

        byte[] result = GC.AllocateUninitializedArray<byte>(length);
        int offset = 0;
        foreach (char character in value)
        {
            if (character is >= '\u0001' and <= '\u007f')
            {
                result[offset++] = (byte)character;
            }
            else if (character <= '\u07ff')
            {
                result[offset++] = (byte)(0xc0 | character >> 6);
                result[offset++] = (byte)(0x80 | character & 0x3f);
            }
            else
            {
                result[offset++] = (byte)(0xe0 | character >> 12);
                result[offset++] = (byte)(0x80 | character >> 6 & 0x3f);
                result[offset++] = (byte)(0x80 | character & 0x3f);
            }
        }

        return result;
    }

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        char[] chars = GC.AllocateUninitializedArray<char>(bytes.Length);
        int byteOffset = 0;
        int charOffset = 0;
        while (byteOffset < bytes.Length)
        {
            byte first = bytes[byteOffset++];
            if (first is >= 0x01 and <= 0x7f)
            {
                chars[charOffset++] = (char)first;
                continue;
            }

            if ((first & 0xe0) == 0xc0)
            {
                byte second = Continuation(bytes, ref byteOffset);
                int value = (first & 0x1f) << 6 | second & 0x3f;
                if (value != 0 && value < 0x80) throw Invalid();
                chars[charOffset++] = (char)value;
                continue;
            }

            if ((first & 0xf0) == 0xe0)
            {
                byte second = Continuation(bytes, ref byteOffset);
                byte third = Continuation(bytes, ref byteOffset);
                int value = (first & 0x0f) << 12 | (second & 0x3f) << 6 | third & 0x3f;
                if (value < 0x800) throw Invalid();
                chars[charOffset++] = (char)value;
                continue;
            }

            throw Invalid();
        }

        return new string(chars, 0, charOffset);

        static byte Continuation(ReadOnlySpan<byte> source, ref int offset)
        {
            if ((uint)offset >= (uint)source.Length || (source[offset] & 0xc0) != 0x80) throw Invalid();
            return source[offset++];
        }

        static FormatException Invalid() => new("Invalid Modified UTF-8 data.");
    }
}

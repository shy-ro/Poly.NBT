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
            _ => throw new InvalidOperationException(),
        };

        lengths.WriteStringLength(stream, bytes.Length);
        stream.Write(bytes);
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

using System.Globalization;
using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

internal enum SnbtNumberStatus
{
    /// <summary>The token is not a number literal at all.</summary>
    NotANumber,

    /// <summary>The token is a valid number literal.</summary>
    Success,

    /// <summary>The token looks like a number literal but is malformed.</summary>
    Invalid,
}

/// <summary>Interprets SNBT numeric literals, including the 1.21.5 extended forms.</summary>
internal static partial class SnbtNumbers
{
    /// <summary>Size of the stack buffer a floating-point literal is normalized into before it is parsed.</summary>
    private const int StackBufferChars = 256;

    public static SnbtNumberStatus TryParse(ReadOnlySpan<char> token, SnbtOptions options, out NbtElement? value, out string? error)
    {
        value = null;
        error = null;
        if (token.Length == 0) return SnbtNumberStatus.NotANumber;

        if (TryParseSpecialFloat(token, out value)) return SnbtNumberStatus.Success;

        if (!IsNumberCandidate(token)) return SnbtNumberStatus.NotANumber;

        int index = 0;
        bool negative = false;
        if (token[0] is '+' or '-')
        {
            negative = token[0] == '-';
            index++;
        }

        int radix = 10;
        if (index + 1 < token.Length && token[index] == '0')
        {
            char marker = token[index + 1];
            if (marker is 'x' or 'X' && IsHexDigit(token, index + 2)) radix = 16;
            else if (marker is 'b' or 'B' && IsBinaryDigit(token, index + 2)) radix = 2;

            if (radix != 10)
            {
                if (!options.AllowBinaryAndHexLiterals) return Invalid("Hexadecimal and binary literals are not allowed.", out error);
                index += 2;
            }
        }

        return radix == 10
            ? ParseDecimal(token, index, negative, options, out value, out error)
            : ParseBased(token, index, radix, negative, options, out value, out error);
    }

    private static SnbtNumberStatus ParseBased(ReadOnlySpan<char> token, int index, int radix, bool negative, SnbtOptions options, out NbtElement? value, out string? error)
    {
        value = null;
        error = null;

        int digitStart = index;
        ulong magnitude = 0;
        bool overflow = false;
        while (index < token.Length && (DigitValue(token[index], radix) >= 0 || token[index] == '_'))
        {
            int digit = DigitValue(token[index], radix);
            if (digit >= 0)
            {
                if (magnitude > (ulong.MaxValue - (ulong)digit) / (ulong)radix) overflow = true;
                else magnitude = magnitude * (ulong)radix + (ulong)digit;
            }

            index++;
        }

        if (index == digitStart) return Invalid("A radix prefix must be followed by at least one digit.", out error);
        if (CheckUnderscores(token, digitStart, index, options, out error) is { } underscoreStatus) return underscoreStatus;
        if (overflow) return Invalid($"The value in '{token}' is out of range.", out error);

        if (SplitSuffix(token, ref index, options, out char? typeLetter, out bool? unsignedSuffix, out error) is { } suffixStatus) return suffixStatus;
        if (index != token.Length) return Invalid($"'{token}' is not a valid number literal.", out error);

        NbtTagType type = typeLetter switch
        {
            'b' or 'B' => NbtTagType.Byte,
            's' or 'S' => NbtTagType.Short,
            'l' or 'L' => NbtTagType.Long,
            null => NbtTagType.Int,
            _ => NbtTagType.End,
        };

        if (type is NbtTagType.End) return Invalid("Hexadecimal and binary literals must be integers ('b', 's', or 'L' suffixes only).", out error);

        // Radix-prefixed values default to unsigned when no signedness suffix is present, but a leading minus
        // sign selects the signed form: -0xFF is -255 rather than an error about an unsigned literal.
        return StoreInteger(magnitude, negative, unsignedSuffix ?? !negative, type, token, out value, out error);
    }

    private static SnbtNumberStatus StoreInteger(ulong magnitude, bool negative, bool unsigned, NbtTagType type, ReadOnlySpan<char> token, out NbtElement? value, out string? error)
    {
        value = null;
        error = null;

        int bits = type switch
        {
            NbtTagType.Byte => 8,
            NbtTagType.Short => 16,
            NbtTagType.Int => 32,
            _ => 64,
        };

        long signedValue;
        if (unsigned)
        {
            if (negative && magnitude != 0) return Invalid($"The unsigned literal '{token}' cannot be negative.", out error);
            ulong limit = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
            if (magnitude > limit) return Invalid($"The value in '{token}' is out of range.", out error);
            signedValue = unchecked((long)magnitude);
        }
        else
        {
            ulong limit = negative ? 1UL << (bits - 1) : (1UL << (bits - 1)) - 1;
            if (magnitude > limit) return Invalid($"The value in '{token}' is out of range.", out error);
            signedValue = negative ? unchecked((long)(0UL - magnitude)) : (long)magnitude;
        }

        value = type switch
        {
            NbtTagType.Byte => new NbtByte(unchecked((sbyte)signedValue)),
            NbtTagType.Short => new NbtShort(unchecked((short)signedValue)),
            NbtTagType.Int => new NbtInt(unchecked((int)signedValue)),
            _ => new NbtLong(signedValue),
        };
        return SnbtNumberStatus.Success;
    }
}

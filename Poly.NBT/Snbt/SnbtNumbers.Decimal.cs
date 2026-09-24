using System.Globalization;
using System.Text;
using Poly.NBT.Dom;

namespace Poly.NBT.Snbt;

internal static partial class SnbtNumbers
{
    private static SnbtNumberStatus ParseDecimal(ReadOnlySpan<char> token, int index, bool negative, SnbtOptions options, out NbtElement? value, out string? error)
    {
        value = null;
        error = null;

        int integerStart = index;
        int integerDigits = 0;
        while (index < token.Length && (IsAsciiDigit(token[index]) || token[index] == '_'))
        {
            if (IsAsciiDigit(token[index])) integerDigits++;
            index++;
        }

        if (CheckUnderscores(token, integerStart, index, options, out error) is { } intStatus) return intStatus;

        bool hasDot = false;
        int fractionDigits = 0;
        if (index < token.Length && token[index] == '.')
        {
            hasDot = true;
            index++;
            int fractionStart = index;
            while (index < token.Length && (IsAsciiDigit(token[index]) || token[index] == '_'))
            {
                if (IsAsciiDigit(token[index])) fractionDigits++;
                index++;
            }

            if (CheckUnderscores(token, fractionStart, index, options, out error) is { } fractionStatus) return fractionStatus;
        }

        bool hasExponent = false;
        if (index < token.Length && token[index] is 'e' or 'E' && HasExponentBody(token, index + 1))
        {
            if (!options.AllowScientificNotation) return Invalid("Scientific notation is not allowed.", out error);
            hasExponent = true;
            index++;
            if (index < token.Length && token[index] is '+' or '-') index++;
            int exponentStart = index;
            while (index < token.Length && (IsAsciiDigit(token[index]) || token[index] == '_')) index++;
            if (CheckUnderscores(token, exponentStart, index, options, out error) is { } exponentStatus) return exponentStatus;
        }

        if (integerDigits + fractionDigits == 0) return Invalid($"'{token}' has no digits.", out error);
        if (hasDot && !options.AllowOmittedFloatParts && (integerDigits == 0 || fractionDigits == 0))
            return Invalid("A fractional literal requires digits on both sides of the decimal point.", out error);

        int numberEnd = index;
        if (SplitSuffix(token, ref index, options, out char? typeLetter, out bool? unsignedSuffix, out error) is { } suffixStatus) return suffixStatus;
        if (index != token.Length) return Invalid($"'{token}' is not a valid number literal.", out error);

        if (typeLetter is 'f' or 'F' or 'd' or 'D' || hasDot || hasExponent)
        {
            if (unsignedSuffix is not null) return Invalid("Signedness suffixes apply to integers only.", out error);
            return ParseFloatValue(token, numberEnd, typeLetter is 'f' or 'F', out value, out error);
        }

        int firstDigit = token[0] is '+' or '-' ? 1 : 0;
        if (options.AllowBinaryAndHexLiterals && integerDigits > 1 && token[firstDigit] == '0')
            return Invalid($"'{token}' has a redundant leading zero.", out error);

        ulong magnitude = 0;
        for (int i = firstDigit; i < numberEnd; i++)
        {
            if (token[i] == '_') continue;
            if (magnitude > (ulong.MaxValue - (ulong)(token[i] - '0')) / 10)
                return Invalid($"The value in '{token}' is out of range.", out error);
            magnitude = magnitude * 10 + (ulong)(token[i] - '0');
        }

        NbtTagType type = typeLetter switch
        {
            'b' or 'B' => NbtTagType.Byte,
            's' or 'S' => NbtTagType.Short,
            'l' or 'L' => NbtTagType.Long,
            _ => NbtTagType.Int,
        };

        // Decimal integers default to signed when no signedness suffix is present.
        return StoreInteger(magnitude, negative, unsignedSuffix ?? false, type, token, out value, out error);
    }

    private static SnbtNumberStatus ParseFloatValue(ReadOnlySpan<char> token, int numberEnd, bool singlePrecision, out NbtElement? value, out string? error)
    {
        value = null;
        error = null;

        var builder = new StringBuilder(numberEnd);
        for (int i = 0; i < numberEnd; i++)
        {
            if (token[i] != '_') builder.Append(token[i]);
        }

        string text = builder.ToString();
        int signLength = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        if (text.Length > signLength && text[signLength] == '.') text = text.Insert(signLength, "0");
        if (text.Length > 0 && text[^1] == '.') text += "0";
        if (text.StartsWith('+')) text = text[1..];

        if (singlePrecision)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float single) || float.IsInfinity(single))
            {
                error = $"The value in '{token}' is out of range.";
                return SnbtNumberStatus.Invalid;
            }

            value = new NbtFloat(single);
            return SnbtNumberStatus.Success;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsInfinity(number))
        {
            error = $"The value in '{token}' is out of range.";
            return SnbtNumberStatus.Invalid;
        }

        value = new NbtDouble(number);
        return SnbtNumberStatus.Success;
    }

    private static SnbtNumberStatus? SplitSuffix(ReadOnlySpan<char> token, ref int index, SnbtOptions options, out char? typeLetter, out bool? unsignedSuffix, out string? error)
    {
        typeLetter = null;
        unsignedSuffix = null;
        error = null;

        int start = index;
        int count = 0;
        while (index < token.Length && count < 2 && SuffixLetters.Contains(token[index]))
        {
            index++;
            count++;
        }

        if (count == 0) return null;

        if (count == 2)
        {
            if (!options.AllowSignednessSuffixes)
            {
                error = "Signedness suffixes are not allowed in this dialect.";
                return SnbtNumberStatus.Invalid;
            }

            char sign = token[start];
            if (sign is not ('s' or 'S' or 'u' or 'U'))
            {
                error = $"'{token}' has an invalid signedness suffix '{sign}'.";
                return SnbtNumberStatus.Invalid;
            }

            unsignedSuffix = sign is 'u' or 'U';
            typeLetter = token[start + 1];
        }
        else
        {
            typeLetter = token[start];
        }

        if (typeLetter is 'i' or 'I')
        {
            error = "The 'i'/'I' integer suffix is not supported; integers have no suffix.";
            return SnbtNumberStatus.Invalid;
        }

        if (typeLetter is 'u' or 'U')
        {
            error = $"'{token}' must place a signedness suffix before an integer type suffix.";
            return SnbtNumberStatus.Invalid;
        }

        return null;
    }

    private static SnbtNumberStatus? CheckUnderscores(ReadOnlySpan<char> token, int start, int end, SnbtOptions options, out string? error)
    {
        error = null;
        if (start >= end) return null;

        bool hasUnderscore = false;
        for (int i = start; i < end; i++)
        {
            if (token[i] == '_')
            {
                hasUnderscore = true;
                break;
            }
        }

        if (!hasUnderscore) return null;

        if (!options.AllowUnderscoreSeparators)
        {
            error = "Underscore digit separators are not allowed.";
            return SnbtNumberStatus.Invalid;
        }

        if (token[start] == '_' || token[end - 1] == '_')
        {
            error = "An underscore separator cannot start or end a digit sequence.";
            return SnbtNumberStatus.Invalid;
        }

        return null;
    }

    private static bool TryParseSpecialFloat(ReadOnlySpan<char> token, out NbtElement? value)
    {
        value = null;
        ReadOnlySpan<char> body = token;
        char? suffix = null;
        if (body.Length > 1 && body[^1] is 'f' or 'F' or 'd' or 'D')
        {
            suffix = body[^1];
            body = body[..^1];
        }

        bool negative = false;
        if (body.StartsWith('+')) body = body[1..];
        else if (body.StartsWith('-'))
        {
            negative = true;
            body = body[1..];
        }

        double number;
        if (body.Equals("NaN", StringComparison.OrdinalIgnoreCase)) number = double.NaN;
        else if (body.Equals("Infinity", StringComparison.OrdinalIgnoreCase)) number = negative ? double.NegativeInfinity : double.PositiveInfinity;
        else return false;

        value = suffix is 'f' or 'F' ? new NbtFloat((float)number) : new NbtDouble(number);
        return true;
    }

    private static bool IsNumberCandidate(ReadOnlySpan<char> token)
    {
        char first = token[0];
        if (IsAsciiDigit(first)) return true;
        return first is '+' or '-' or '.' && token.Length > 1 && IsAsciiDigit(token[1]);
    }

    private static bool HasExponentBody(ReadOnlySpan<char> token, int index)
    {
        if (index < token.Length && token[index] is '+' or '-') index++;
        return index < token.Length && (IsAsciiDigit(token[index]) || token[index] == '_');
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool IsHexDigit(ReadOnlySpan<char> token, int index) => index < token.Length && SnbtLexer.HexValue(token[index]) >= 0;

    private static bool IsBinaryDigit(ReadOnlySpan<char> token, int index) => index < token.Length && token[index] is '0' or '1';

    private static int DigitValue(char value, int radix)
    {
        int digit = SnbtLexer.HexValue(value);
        return digit >= 0 && digit < radix ? digit : -1;
    }

    private static SnbtNumberStatus Invalid(string message, out string? error)
    {
        error = message;
        return SnbtNumberStatus.Invalid;
    }
}

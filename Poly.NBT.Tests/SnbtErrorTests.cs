using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtErrorTests
{
    [Fact]
    public void ReportsDuplicateCompoundKeys()
    {
        Assert.Equal(9, Offset("{a:1,b:2,a:3}"));
        Assert.Equal(5, Offset("{a:1,\"a\":2}"));
    }

    [Fact]
    public void ReportsMalformedNumbersAtTheirStart()
    {
        Assert.Equal(0, Offset("1x"));
        Assert.Equal(0, Offset("1_2_"));
        Assert.Equal(0, Offset("123i"));
        Assert.Equal(0, Offset("82u"));
        Assert.Equal(0, Offset("30bu"));
        Assert.Equal(0, Offset("-87uI"));
        Assert.Equal(0, Offset("256b"));
        Assert.Equal(0, Offset("253sb"));
        Assert.Equal(0, Offset("32768s"));
        Assert.Equal(0, Offset("1e1000"));
        Assert.Equal(0, Offset("0x"));
        Assert.Equal(0, Offset("0123"));

        // A minus sign selects the signed form, so only unsigned suffixes still reject it, and the signed form
        // keeps its range check: -255 does not fit a signed byte.
        Assert.Equal(0, Offset("-0xFFub"));
        Assert.Equal(0, Offset("-0xFFsb"));
        Assert.Equal(0, Offset("-0x80000001"));
    }

    [Fact]
    public void ReportsStructuralErrors()
    {
        Assert.Equal(1, Offset("{"));
        Assert.Equal(2, Offset("{a"));
        Assert.Equal(1, Offset("[,]"));
        Assert.Equal(4, Offset("[1,2"));
        Assert.Equal(7, Offset("[1,2,3,,4]"));
        Assert.Equal(4, Offset("[I;1"));
    }

    [Fact]
    public void ReportsStringErrors()
    {
        Assert.Equal(0, Offset("\"abc"));
        Assert.Equal(2, Offset("\"a\\qb\""));
        Assert.Equal(2, Offset("'a\\nb'"));
        Assert.Equal(1, Offset("\"\\u00zz\""));
    }

    [Fact]
    public void ReportsArrayElementErrors()
    {
        Assert.Equal(5, Offset("[I;1,2.5]"));
        Assert.Equal(5, Offset("[B;1,200]"));
        Assert.Equal(3, Offset("[L;nope]"));
    }

    [Fact]
    public void ReportsOperationErrors()
    {
        Assert.Equal(0, Offset("bool(\"foo\")"));
        Assert.Equal(0, Offset("uuid(\"nope\")"));
        Assert.Equal(7, Offset("bool(1)extra"));
    }

    [Fact]
    public void ReportsTrailingContentAndMissingValues()
    {
        Assert.Equal(2, Offset("1 2"));
        Assert.Equal(0, Offset(string.Empty));
        Assert.Equal(2, Offset("  "));
    }

    [Fact]
    public void ParseExceptionIsAFormatException()
    {
        SnbtParseException exception = Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("{"));
        Assert.IsAssignableFrom<FormatException>(exception);
        Assert.NotEqual(0, exception.Message.Length);
    }

    private static int Offset(string text)
        => Assert.Throws<SnbtParseException>(() => SnbtParser.Parse(text)).Offset;
}

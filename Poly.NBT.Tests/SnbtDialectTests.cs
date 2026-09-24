using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtDialectTests
{
    [Fact]
    public void ClassicDialectParsesClassicSyntax()
    {
        Assert.Equal(new NbtByte(-16), SnbtParser.Parse("-16b", SnbtOptions.v1_13));
        Assert.Equal(new NbtShort(15), SnbtParser.Parse("15s", SnbtOptions.v1_13));
        Assert.Equal(new NbtLong(15), SnbtParser.Parse("15L", SnbtOptions.v1_13));
        Assert.Equal(new NbtDouble(1.5), SnbtParser.Parse("1.5d", SnbtOptions.v1_13));
        Assert.Equal(new NbtFloat(1.5f), SnbtParser.Parse("1.5f", SnbtOptions.v1_13));
        Assert.Equal("{a:1,b:[1,2]}", SnbtWriter.Write(SnbtParser.Parse("{a:1,b:[1,2]}", SnbtOptions.v1_13)));
        Assert.Equal("[B;1b,2b]", SnbtWriter.Write(SnbtParser.Parse("[B;1b,2b]", SnbtOptions.v1_13)));
    }

    [Fact]
    public void BooleanLiteralsAreDialectSpecific()
    {
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("true", SnbtOptions.v1_21_5));
        Assert.Equal(new NbtByte(0), SnbtParser.Parse("false", SnbtOptions.v1_21_5));
        Assert.Equal(new NbtString("true"), SnbtParser.Parse("true", SnbtOptions.v1_13));
    }

    [Fact]
    public void RadixPrefixesAreDialectSpecific()
    {
        Assert.Equal(new NbtInt(255), SnbtParser.Parse("0xFF", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("0xFF", SnbtOptions.v1_13));
    }

    [Fact]
    public void ScientificNotationIsDialectSpecific()
    {
        Assert.Equal(new NbtDouble(1500d), SnbtParser.Parse("1.5e3", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("1.5e3", SnbtOptions.v1_13));
    }

    [Fact]
    public void OmittedFloatPartsAreDialectSpecific()
    {
        Assert.Equal(new NbtDouble(0.5), SnbtParser.Parse(".5", SnbtOptions.v1_21_5));
        Assert.Equal(new NbtDouble(5d), SnbtParser.Parse("5.", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse(".5", SnbtOptions.v1_13));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("5.", SnbtOptions.v1_13));
    }

    [Fact]
    public void TrailingCommasAreDialectSpecific()
    {
        Assert.Equal("{a:1}", SnbtWriter.Write(SnbtParser.Parse("{a:1,}", SnbtOptions.v1_21_5)));
        Assert.Equal("[1]", SnbtWriter.Write(SnbtParser.Parse("[1,]", SnbtOptions.v1_21_5)));
        Assert.Equal("[I;1]", SnbtWriter.Write(SnbtParser.Parse("[I;1,]", SnbtOptions.v1_21_5)));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("{a:1,}", SnbtOptions.v1_13));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("[1,]", SnbtOptions.v1_13));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("[I;1,]", SnbtOptions.v1_13));
    }

    [Fact]
    public void HeterogeneousListsAreDialectSpecific()
    {
        Assert.Equal("[1,two,{a:1}]", SnbtWriter.Write(SnbtParser.Parse("[1,two,{a:1}]", SnbtOptions.v1_21_5)));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("[1,two]", SnbtOptions.v1_13));
    }

    [Fact]
    public void UnderscoreSeparatorsAreDialectSpecific()
    {
        Assert.Equal(new NbtInt(1000), SnbtParser.Parse("1_000", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("1_000", SnbtOptions.v1_13));
    }

    [Fact]
    public void SignednessSuffixesAreDialectSpecific()
    {
        Assert.Equal(new NbtByte(-16), SnbtParser.Parse("240ub", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("240ub", SnbtOptions.v1_13));
    }

    [Fact]
    public void OperationsAreDialectSpecific()
    {
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("bool(5)", SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("bool(5)", SnbtOptions.v1_13));
    }

    [Fact]
    public void ParameterlessOverloadsUseTheModernDialect()
    {
        Assert.Equal(new NbtInt(255), SnbtParser.Parse("0xFF"));
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("true"));
    }
}

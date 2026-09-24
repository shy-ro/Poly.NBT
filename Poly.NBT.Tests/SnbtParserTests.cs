using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtParserTests
{
    [Fact]
    public void ParsesScalarsWithTypeSuffixes()
    {
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("1b"));
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("1B"));
        Assert.Equal(new NbtShort(1), SnbtParser.Parse("1s"));
        Assert.Equal(new NbtShort(1), SnbtParser.Parse("1S"));
        Assert.Equal(new NbtInt(1), SnbtParser.Parse("1"));
        Assert.Equal(new NbtLong(1), SnbtParser.Parse("1L"));
        Assert.Equal(new NbtLong(1), SnbtParser.Parse("1l"));
        Assert.Equal(new NbtFloat(1f), SnbtParser.Parse("1.0f"));
        Assert.Equal(new NbtFloat(1f), SnbtParser.Parse("1.0F"));
        Assert.Equal(new NbtDouble(1d), SnbtParser.Parse("1.0d"));
        Assert.Equal(new NbtDouble(1d), SnbtParser.Parse("1.0D"));
        Assert.Equal(new NbtDouble(1.5), SnbtParser.Parse("1.5"));
    }

    [Fact]
    public void ParsesSignedAndUnsignedSuffixes()
    {
        Assert.Equal(new NbtByte(-16), SnbtParser.Parse("-16b"));
        Assert.Equal(new NbtByte(-16), SnbtParser.Parse("-16sb"));
        Assert.Equal(new NbtByte(-16), SnbtParser.Parse("240uB"));
        Assert.Equal(new NbtShort(15), SnbtParser.Parse("15s"));
        Assert.Equal(new NbtShort(15), SnbtParser.Parse("15sS"));
        Assert.Equal(new NbtShort(15), SnbtParser.Parse("15Us"));
        Assert.Equal(new NbtByte(17), SnbtParser.Parse("0x11sb"));
        Assert.Equal(new NbtByte(17), SnbtParser.Parse("0x11ub"));
        Assert.Equal(new NbtByte(127), SnbtParser.Parse("0b1111111sb"));
        Assert.Equal(new NbtByte(-1), SnbtParser.Parse("0b11111111ub"));
    }

    [Fact]
    public void ParsesRadixPrefixedIntegers()
    {
        Assert.Equal(new NbtInt(2989), SnbtParser.Parse("0xbad"));
        Assert.Equal(new NbtInt(51966), SnbtParser.Parse("0xCAFE"));
        Assert.Equal(new NbtInt(5), SnbtParser.Parse("0b101"));
        Assert.Equal(new NbtInt(2619), SnbtParser.Parse("0xA3b"));
        Assert.Equal(new NbtInt(1000), SnbtParser.Parse("0x3E8"));
    }

    [Fact]
    public void ParsesUnderscoreSeparators()
    {
        Assert.Equal(new NbtInt(1000), SnbtParser.Parse("1_000"));
        Assert.Equal(new NbtInt(5), SnbtParser.Parse("0b1_01"));
        Assert.Equal(new NbtInt(255), SnbtParser.Parse("0xF_F"));
        Assert.Equal(new NbtLong(1_000_000_000_000L), SnbtParser.Parse("1_000_000_000_000L"));
        Assert.Equal(new NbtFloat(1000.5f), SnbtParser.Parse("1_000.5f"));
    }

    [Fact]
    public void ParsesSpecialFloats()
    {
        Assert.Equal(new NbtDouble(double.NaN), SnbtParser.Parse("NaN"));
        Assert.Equal(new NbtFloat(float.NaN), SnbtParser.Parse("NaNf"));
        Assert.Equal(new NbtDouble(double.PositiveInfinity), SnbtParser.Parse("Infinity"));
        Assert.Equal(new NbtDouble(double.NegativeInfinity), SnbtParser.Parse("-Infinity"));
        Assert.Equal(new NbtFloat(float.NegativeInfinity), SnbtParser.Parse("-Infinityf"));
    }

    [Fact]
    public void ParsesOmittedFloatParts()
    {
        Assert.Equal(new NbtDouble(0.5), SnbtParser.Parse(".5"));
        Assert.Equal(new NbtDouble(5d), SnbtParser.Parse("5."));
    }

    [Fact]
    public void ParsesStringsAndEscapes()
    {
        Assert.Equal(new NbtString("hello"), SnbtParser.Parse("hello"));
        Assert.Equal(new NbtString("hello.world"), SnbtParser.Parse("hello.world"));
        Assert.Equal(new NbtString(string.Empty), SnbtParser.Parse("''"));
        Assert.Equal(new NbtString(string.Empty), SnbtParser.Parse("\"\""));
        Assert.Equal(new NbtString("a\"b"), SnbtParser.Parse("'a\"b'"));
        Assert.Equal(new NbtString("a'b"), SnbtParser.Parse("\"a'b\""));
        Assert.Equal(new NbtString("line\nbreak"), SnbtParser.Parse("\"line\\nbreak\""));
        Assert.Equal(new NbtString("tab\there"), SnbtParser.Parse("\"tab\\there\""));
        Assert.Equal(new NbtString("carriage\rreturn"), SnbtParser.Parse("\"carriage\\rreturn\""));
        Assert.Equal(new NbtString("back\\slash"), SnbtParser.Parse("\"back\\\\slash\""));
        Assert.Equal(new NbtString("A"), SnbtParser.Parse("\"\\u0041\""));
        Assert.Equal(new NbtString("it's"), SnbtParser.Parse("'it\\'s'"));
        Assert.Equal(new NbtString("with space"), SnbtParser.Parse("\"with space\""));
    }

    [Fact]
    public void ParsesContainersAndPreservesKeyOrder()
    {
        const string text = "{name:Bananrama,Health:20b,nested:{a:1},list:[1,2]}";
        Assert.Equal(text, SnbtWriter.Write(SnbtParser.Parse(text)));
    }

    [Fact]
    public void ParsesNumericAndBooleanKeysAsStrings()
    {
        NbtCompound compound = Assert.IsType<NbtCompound>(SnbtParser.Parse("{1:\"one\",2.5:\"two\",true:\"three\"}"));
        Assert.Equal(new NbtString("one"), compound["1"]);
        Assert.Equal(new NbtString("two"), compound["2.5"]);
        Assert.Equal(new NbtString("three"), compound["true"]);
    }

    [Fact]
    public void ParsesEmptyContainers()
    {
        Assert.Equal("{}", SnbtWriter.Write(SnbtParser.Parse("{}")));
        Assert.Equal("[]", SnbtWriter.Write(SnbtParser.Parse("[]")));
        Assert.Equal("[B;]", SnbtWriter.Write(SnbtParser.Parse("[B;]")));
        Assert.Equal("[I;]", SnbtWriter.Write(SnbtParser.Parse("[I;]")));
        Assert.Equal("[L;]", SnbtWriter.Write(SnbtParser.Parse("[L;]")));
    }

    [Fact]
    public void ParsesTypedArrays()
    {
        Assert.Equal(new NbtByteArray([1, 2, 3]), SnbtParser.Parse("[B;1b,2b,3b]"));
        Assert.Equal(new NbtIntArray([1, 2, 3]), SnbtParser.Parse("[I;1,2,3]"));
        Assert.Equal(new NbtLongArray([1L, 2L, 3L]), SnbtParser.Parse("[L;1L,2L,3L]"));
    }

    [Fact]
    public void ArrayElementsIgnoreMismatchedSuffixes()
    {
        Assert.Equal(new NbtByteArray([1, 2, 3]), SnbtParser.Parse("[B;1,2s,3L]"));
        Assert.Equal(new NbtIntArray([1, 2]), SnbtParser.Parse("[I;1b,2s]"));
        Assert.Equal(new NbtLongArray([1L, 2L]), SnbtParser.Parse("[L;1b,2]"));
    }

    [Fact]
    public void ParsesOperations()
    {
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("bool(5)"));
        Assert.Equal(new NbtByte(0), SnbtParser.Parse("bool(0)"));
        Assert.Equal(new NbtByte(1), SnbtParser.Parse("bool(true)"));
        Assert.Equal(new NbtByte(0), SnbtParser.Parse("bool(false)"));
        Assert.Equal(
            new NbtIntArray([-132296786, 2112623056, -1486552928, -920753162]),
            SnbtParser.Parse("uuid(\"f81d4fae-7dec-11d0-a765-00a0c91e6bf6\")"));
    }

    [Fact]
    public void RoundTripsRichDocument()
    {
        const string text = "{name:Bananrama,Health:20b,Pos:[1.0d,2.5d,3.0d],Data:[B;1b,2b],Ids:[I;1,2],"
            + "Longs:[L;1L],Mix:[1,\"two three\",{a:1}],Flag:1b,Count:5s,Big:7L,Empty:{},EmptyList:[]}";
        NbtElement element = SnbtParser.Parse(text);

        Assert.Equal(text, SnbtWriter.Write(element));
        Assert.Equal(element, SnbtParser.Parse(SnbtWriter.Write(element)));
    }

    [Fact]
    public void ReadsDocumentsAndTextReaders()
    {
        NbtDocument document = SnbtParser.ParseDocument(new StringReader("{a:1}"));
        Assert.Equal(string.Empty, document.RootTagName);
        Assert.Equal("{a:1}", SnbtWriter.Write(document));

        using var writer = new StringWriter();
        SnbtWriter.Write(writer, document.RootElement);
        Assert.Equal("{a:1}", writer.ToString());
    }

    [Fact]
    public void NormalizesMinecraftStyleInput()
    {
        // Example from the Minecraft Wiki "NBT format" page. Quotes are dropped where a bare string is unambiguous.
        const string source = "{name1:123,name2:\"sometext1\",name3:{subname1:456,subname2:\"sometext2\"}}";
        Assert.Equal("{name1:123,name2:sometext1,name3:{subname1:456,subname2:sometext2}}", SnbtWriter.Write(SnbtParser.Parse(source)));

        // Array spellings emitted by Minecraft's StringTagVisitor; lowercase type suffixes normalize to its casing.
        Assert.Equal("[B;1b,2b,3b]", SnbtWriter.Write(SnbtParser.Parse("[B;1b,2b,3b]")));
        Assert.Equal("[I;1,2,3]", SnbtWriter.Write(SnbtParser.Parse("[I;1,2,3]")));
        Assert.Equal("[L;1L,2L,3L]", SnbtWriter.Write(SnbtParser.Parse("[L;1l,2l,3l]")));

        // Text components keep quoted strings quoted and bare names bare.
        Assert.Equal("{text:\"Hello world\",color:red,bold:1b}", SnbtWriter.Write(SnbtParser.Parse("{text:\"Hello world\",color:red,bold:1b}")));

        // Boolean literals normalize to the byte values Minecraft stores.
        Assert.Equal("{a:1b}", SnbtWriter.Write(SnbtParser.Parse("{a:true}")));
    }
}

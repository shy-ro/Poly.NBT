using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtWriterTests
{
    [Fact]
    public void WritesNumericTypeSuffixes()
    {
        Assert.Equal("1b", SnbtWriter.Write(new NbtByte(1)));
        Assert.Equal("1s", SnbtWriter.Write(new NbtShort(1)));
        Assert.Equal("1", SnbtWriter.Write(new NbtInt(1)));
        Assert.Equal("1L", SnbtWriter.Write(new NbtLong(1)));
        Assert.Equal("1.0f", SnbtWriter.Write(new NbtFloat(1f)));
        Assert.Equal("1.0d", SnbtWriter.Write(new NbtDouble(1d)));
        Assert.Equal("1.5f", SnbtWriter.Write(new NbtFloat(1.5f)));
        Assert.Equal("-0.25d", SnbtWriter.Write(new NbtDouble(-0.25)));
        Assert.Equal("-128b", SnbtWriter.Write(new NbtByte(-128)));
    }

    [Fact]
    public void WritesCompactContainers()
    {
        NbtCompound compound = new(new Dictionary<string, NbtElement>
        {
            ["a"] = new NbtInt(1),
            ["b"] = new NbtList(new NbtInt(1), new NbtInt(2)),
            ["c"] = new NbtCompound(new Dictionary<string, NbtElement> { ["d"] = new NbtString("e") }),
        });

        Assert.Equal("{a:1,b:[1,2],c:{d:e}}", SnbtWriter.Write(compound));
        Assert.Equal("{}", SnbtWriter.Write(new NbtCompound(new Dictionary<string, NbtElement>())));
        Assert.Equal("[]", SnbtWriter.Write(new NbtList(new List<NbtElement>())));
    }

    [Fact]
    public void WritesTypedArrays()
    {
        Assert.Equal("[B;1b,2b]", SnbtWriter.Write(new NbtByteArray([1, 2])));
        Assert.Equal("[B;-1b]", SnbtWriter.Write(new NbtByteArray([255])));
        Assert.Equal("[I;1,2]", SnbtWriter.Write(new NbtIntArray([1, 2])));
        Assert.Equal("[L;1L,2L]", SnbtWriter.Write(new NbtLongArray([1L, 2L])));
        Assert.Equal("[B;]", SnbtWriter.Write(new NbtByteArray([])));
    }

    [Fact]
    public void WritesSpecialFloats()
    {
        Assert.Equal("NaNf", SnbtWriter.Write(new NbtFloat(float.NaN)));
        Assert.Equal("NaNd", SnbtWriter.Write(new NbtDouble(double.NaN)));
        Assert.Equal("Infinityd", SnbtWriter.Write(new NbtDouble(double.PositiveInfinity)));
        Assert.Equal("-Infinityd", SnbtWriter.Write(new NbtDouble(double.NegativeInfinity)));
        Assert.Equal("-Infinityf", SnbtWriter.Write(new NbtFloat(float.NegativeInfinity)));
    }

    [Fact]
    public void QuotesStringsOnlyWhenRequired()
    {
        Assert.Equal("hello", SnbtWriter.Write(new NbtString("hello")));
        Assert.Equal("_foo", SnbtWriter.Write(new NbtString("_foo")));
        Assert.Equal("hello.world", SnbtWriter.Write(new NbtString("hello.world")));
        Assert.Equal("\"\"", SnbtWriter.Write(new NbtString(string.Empty)));
        Assert.Equal("\"1\"", SnbtWriter.Write(new NbtString("1")));
        Assert.Equal("\"1b\"", SnbtWriter.Write(new NbtString("1b")));
        Assert.Equal("\"-1\"", SnbtWriter.Write(new NbtString("-1")));
        Assert.Equal("\"0x10\"", SnbtWriter.Write(new NbtString("0x10")));
        Assert.Equal("\"true\"", SnbtWriter.Write(new NbtString("true")));
        Assert.Equal("\"false\"", SnbtWriter.Write(new NbtString("false")));
        Assert.Equal("\"NaN\"", SnbtWriter.Write(new NbtString("NaN")));
        Assert.Equal("\"Infinity\"", SnbtWriter.Write(new NbtString("Infinity")));
        Assert.Equal("\"with space\"", SnbtWriter.Write(new NbtString("with space")));
        Assert.Equal("'a\"b'", SnbtWriter.Write(new NbtString("a\"b")));
        Assert.Equal("\"a'b\"", SnbtWriter.Write(new NbtString("a'b")));
        Assert.Equal("\"a\\nb\"", SnbtWriter.Write(new NbtString("a\nb")));
        Assert.Equal("\"a\\u0000b\"", SnbtWriter.Write(new NbtString("a\0b")));
    }

    [Fact]
    public void QuotesStringsThatStartWithASignOrPoint()
    {
        // SNBT reserves a leading sign or point for numbers. Minecraft's tokenizer tries a numeric parse first
        // and reports an error rather than falling back to a bare string, so {a:-foo} is invalid SNBT even
        // though this library's own parser recovers by falling back. Only the first character is reserved:
        // "a-b" and "hello.world" stay bare.
        Assert.Equal("\"-foo\"", SnbtWriter.Write(new NbtString("-foo")));
        Assert.Equal("\"+foo\"", SnbtWriter.Write(new NbtString("+foo")));
        Assert.Equal("\".foo\"", SnbtWriter.Write(new NbtString(".foo")));
        Assert.Equal("\"-1.5\"", SnbtWriter.Write(new NbtString("-1.5")));
        Assert.Equal("a-b", SnbtWriter.Write(new NbtString("a-b")));
        Assert.Equal("a+b", SnbtWriter.Write(new NbtString("a+b")));
        Assert.Equal("a.b", SnbtWriter.Write(new NbtString("a.b")));
        Assert.Equal("_foo", SnbtWriter.Write(new NbtString("_foo")));
    }

    [Fact]
    public void PicksTheQuoteCharacterThatNeedsNoEscaping()
    {
        Assert.Equal("'a\"b'", SnbtWriter.Write(new NbtString("a\"b")));
        Assert.Equal("\"a'b\"", SnbtWriter.Write(new NbtString("a'b")));

        // With both kinds present there is no escape-free choice, so double quotes win and the double quotes
        // inside are escaped. The alternative - taking the opposite of whichever quote appears first - is the
        // rule the Wiki records for the always-quoted /data get path, which is a different path from this
        // writer's bare-where-possible output. Both forms parse to the same value, which is asserted here
        // rather than assumed.
        Assert.Equal("\"a\\\"b'c\"", SnbtWriter.Write(new NbtString("a\"b'c")));
        Assert.Equal("\"a'b\\\"c\"", SnbtWriter.Write(new NbtString("a'b\"c")));
        Assert.Equal(new NbtString("a\"b'c"), SnbtParser.Parse(SnbtWriter.Write(new NbtString("a\"b'c"))));
    }

    [Fact]
    public void WrittenStringsRoundTrip()
    {
        string[] values = ["hello", "_foo", "hello.world", string.Empty, "1", "1b", "-1", "0x10", "true", "false",
            "NaN", "Infinity", "with space", "a\"b", "a'b", "a\nb", "a\0b", "-foo", ".foo", "a+b", "a-b", "a.b", "123abc"];

        foreach (string value in values)
        {
            string text = SnbtWriter.Write(new NbtString(value));
            Assert.Equal(new NbtString(value), SnbtParser.Parse(text));
        }
    }
}

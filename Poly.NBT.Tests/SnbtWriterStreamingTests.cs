using System.Text;
using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtWriterStreamingTests
{
    [Fact]
    public void NeverMaterializesTheWholeDocument()
    {
        NbtCompound compound = new(new Dictionary<string, NbtElement>
        {
            ["a"] = new NbtInt(1),
            ["b"] = new NbtList(new NbtInt(1), new NbtInt(2)),
            ["c"] = new NbtCompound(new Dictionary<string, NbtElement> { ["d"] = new NbtString("e") }),
        });

        const string Expected = "{a:1,b:[1,2],c:{d:e}}";
        var writer = new RecordingWriter();

        SnbtWriter.Write(writer, compound);

        Assert.Equal(Expected, writer.ToString());
        Assert.Equal(Expected, string.Concat(writer.Writes));
        Assert.DoesNotContain(Expected, writer.Writes);
    }

    [Fact]
    public void WritesScalarDigitsAndSuffixSeparately()
    {
        var writer = new RecordingWriter();

        SnbtWriter.Write(writer, new NbtLong(1));

        Assert.Equal(new[] { "1", "L" }, writer.Writes);
    }

    [Fact]
    public void StreamsExpandedDecimalsWithoutBuildingTheLiteral()
    {
        var writer = new RecordingWriter();

        SnbtWriter.Write(writer, new NbtDouble(1e20), SnbtOptions.v1_13);

        Assert.Equal("100000000000000000000.0d", string.Concat(writer.Writes));
        Assert.DoesNotContain("100000000000000000000.0d", writer.Writes);
    }

    [Fact]
    public void StreamsDocumentsSpecialFloatsAndArrays()
    {
        var special = new RecordingWriter();
        SnbtWriter.Write(special, new NbtDocument(string.Empty, new NbtFloat(float.NaN)));
        Assert.Equal("NaNf", string.Concat(special.Writes));

        var array = new RecordingWriter();
        SnbtWriter.Write(array, new NbtDocument(string.Empty, new NbtByteArray([1, 2])));
        Assert.Equal("[B;1b,2b]", string.Concat(array.Writes));

        var strings = new RecordingWriter();
        SnbtWriter.Write(strings, new NbtString("with space"));
        Assert.Equal("\"with space\"", string.Concat(strings.Writes));
    }

    private sealed class RecordingWriter : StringWriter
    {
        public List<string> Writes { get; } = [];

        public override void Write(char value)
        {
            Writes.Add(value.ToString());
            base.Write(value);
        }

        public override void Write(string? value)
        {
            if (value is not null) Writes.Add(value);
            base.Write(value);
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            Writes.Add(buffer.ToString());
            base.Write(buffer);
        }
    }
}

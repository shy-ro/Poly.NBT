using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

public sealed class SnbtWriterDialectTests
{
    [Fact]
    public void ModernDialectWritesScientificNotation()
    {
        Assert.Contains("E", SnbtWriter.Write(new NbtDouble(1e20)));
        Assert.Contains("E", SnbtWriter.Write(new NbtFloat(1e20f)));

        // The modern output is exactly what the modern parser expects, but the classic parser rejects it.
        string text = SnbtWriter.Write(new NbtDouble(1e20));
        Assert.Equal(new NbtDouble(1e20), SnbtParser.Parse(text, SnbtOptions.v1_21_5));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse(text, SnbtOptions.v1_13));
    }

    [Fact]
    public void ClassicDialectWritesPlainDecimals()
    {
        Assert.Equal("100000000000000000000.0d", SnbtWriter.Write(new NbtDouble(1e20), SnbtOptions.v1_13));
        Assert.Equal("0.00000015d", SnbtWriter.Write(new NbtDouble(1.5e-7), SnbtOptions.v1_13));
        Assert.Equal("-100000000000000000000.0d", SnbtWriter.Write(new NbtDouble(-1e20), SnbtOptions.v1_13));
        Assert.Equal("1.0d", SnbtWriter.Write(new NbtDouble(1d), SnbtOptions.v1_13));
        Assert.Equal("0.1d", SnbtWriter.Write(new NbtDouble(0.1d), SnbtOptions.v1_13));
        Assert.Equal("NaNf", SnbtWriter.Write(new NbtFloat(float.NaN), SnbtOptions.v1_13));
    }

    [Fact]
    public void ClassicOutputNeverUsesExponentNotation()
    {
        double[] values = [1e20, 1e-20, -1e20, 1e300, double.Epsilon, double.MaxValue, double.MinValue, 1.0 / 3.0, 0d, -0d];

        foreach (double value in values)
        {
            string text = SnbtWriter.Write(new NbtDouble(value), SnbtOptions.v1_13);
            Assert.DoesNotContain("E", text);
            Assert.DoesNotContain("e", text);
            Assert.Equal(new NbtDouble(value), SnbtParser.Parse(text, SnbtOptions.v1_13));
        }

        float[] singleValues = [1e20f, 1e-20f, -1e20f, float.Epsilon, float.MaxValue, float.MinValue, 1f / 3f];

        foreach (float value in singleValues)
        {
            string text = SnbtWriter.Write(new NbtFloat(value), SnbtOptions.v1_13);
            Assert.DoesNotContain("E", text);
            Assert.DoesNotContain("e", text);
            Assert.Equal(new NbtFloat(value), SnbtParser.Parse(text, SnbtOptions.v1_13));
        }
    }

    [Fact]
    public void OutputRoundTripsUnderItsOwnDialect()
    {
        NbtCompound compound = new(new Dictionary<string, NbtElement>
        {
            ["d"] = new NbtDouble(1e20),
            ["f"] = new NbtFloat(-2.5e-9f),
            ["s"] = new NbtString(".5"),
            ["t"] = new NbtString("true"),
            ["n"] = new NbtString("1_000"),
            ["l"] = new NbtList(new NbtInt(1), new NbtInt(2)),
        });

        foreach (SnbtOptions options in new[] { SnbtOptions.v1_13, SnbtOptions.v1_21_5 })
        {
            string text = SnbtWriter.Write(compound, options);
            Assert.Equal(compound, SnbtParser.Parse(text, options));
        }
    }

    [Fact]
    public void QuotesNumberLikeStringsInEveryDialect()
    {
        // ".5" is a number only with AllowOmittedFloatParts, but the classic dialect rejects it as a malformed
        // number rather than reading it as a bare string, so quoting cannot follow the output dialect.
        Assert.Equal("{a:\".5\"}", SnbtWriter.Write(CompoundWithString("a", ".5"), SnbtOptions.v1_13));
        Assert.Equal("{a:\".5\"}", SnbtWriter.Write(CompoundWithString("a", ".5"), SnbtOptions.v1_21_5));
        Assert.Equal("{a:\"1\"}", SnbtWriter.Write(CompoundWithString("a", "1"), SnbtOptions.v1_13));
    }

    [Fact]
    public void WritesThroughTextWriterWithDialect()
    {
        using var writer = new StringWriter();
        SnbtWriter.Write(writer, new NbtDouble(1e20), SnbtOptions.v1_13);
        Assert.Equal("100000000000000000000.0d", writer.ToString());

        using var documentWriter = new StringWriter();
        SnbtWriter.Write(documentWriter, new NbtDocument(string.Empty, new NbtInt(1)), SnbtOptions.v1_13);
        Assert.Equal("1", documentWriter.ToString());
    }

    private static NbtCompound CompoundWithString(string key, string value)
        => new(new Dictionary<string, NbtElement> { [key] = new NbtString(value) });
}

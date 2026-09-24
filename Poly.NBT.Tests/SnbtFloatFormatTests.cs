using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

/// <summary>
/// Pins floating-point text against the shape Java's <c>Double.toString</c>/<c>Float.toString</c> produces,
/// because that is what Minecraft prints for the same value.
/// </summary>
/// <remarks>
/// A different shape is not a parse error - Java reads <c>1E+20d</c> and <c>1.0E20d</c> alike - but it is a
/// stable false positive in any diff, checksum, or cache key computed over SNBT output, so the shapes are
/// aligned. The values below are the Java-documented outputs for the same bit patterns.
/// </remarks>
public sealed class SnbtFloatFormatTests
{
    public static TheoryData<double, string> DoubleLiterals => new()
    {
        { 1e20, "1.0E20d" },
        { 1e-20, "1.0E-20d" },
        { 1e300, "1.0E300d" },
        { -1e20, "-1.0E20d" },
        { 1.2345678901234568e17, "1.2345678901234568E17d" },
        { double.MaxValue, "1.7976931348623157E308d" },
        { double.MinValue, "-1.7976931348623157E308d" },

        // Java's threshold is [1e-3, 1e7); the runtime's own "R" format stays plain until 1e15.
        { 1e7, "1.0E7d" },
        { 1e8, "1.0E8d" },
        { 1e-4, "1.0E-4d" },
        { 1.5e-7, "1.5E-7d" },
        { 9999999d, "9999999.0d" },
        { 1e-3, "0.001d" },

        // The mantissa keeps a point and one fractional digit even when the value is an integer.
        { 0d, "0.0d" },
        { -0d, "-0.0d" },
        { 1d, "1.0d" },
        { 100d, "100.0d" },
        { 150d, "150.0d" },
        { 1234567d, "1234567.0d" },
        { 12345678d, "1.2345678E7d" },
        { 0.1d, "0.1d" },
        { -0.25d, "-0.25d" },
        { 1d / 3d, "0.3333333333333333d" },
    };

    public static TheoryData<float, string> FloatLiterals => new()
    {
        { 1e20f, "1.0E20f" },
        { 1e-20f, "1.0E-20f" },
        { float.MaxValue, "3.4028235E38f" },
        { float.MinValue, "-3.4028235E38f" },
        { 1e7f, "1.0E7f" },
        { 1e-4f, "1.0E-4f" },
        { 1.5e-7f, "1.5E-7f" },
        { -2.5e-9f, "-2.5E-9f" },
        { 1f, "1.0f" },
        { 1234567f, "1234567.0f" },
        { 12345678f, "1.2345678E7f" },
        { 0.1f, "0.1f" },
        { 1f / 3f, "0.33333334f" },
    };

    public static TheoryData<double, string> ClassicDoubleLiterals => new()
    {
        { 1e20, "100000000000000000000.0d" },
        { -1e20, "-100000000000000000000.0d" },
        { 1e7, "10000000.0d" },
        { 12345678d, "12345678.0d" },
        { 1e-4, "0.0001d" },
        { 1.5e-7, "0.00000015d" },
        { 1e-20, "0.00000000000000000001d" },
        { 1d, "1.0d" },
        { 0.1d, "0.1d" },
        { 0d, "0.0d" },
    };

    [Theory]
    [MemberData(nameof(DoubleLiterals))]
    public void DoubleLiteralsMatchTheJavaShape(double value, string expected)
        => Assert.Equal(expected, SnbtWriter.Write(new NbtDouble(value), SnbtOptions.v1_21_5));

    [Theory]
    [MemberData(nameof(FloatLiterals))]
    public void FloatLiteralsMatchTheJavaShape(float value, string expected)
        => Assert.Equal(expected, SnbtWriter.Write(new NbtFloat(value), SnbtOptions.v1_21_5));

    [Theory]
    [MemberData(nameof(ClassicDoubleLiterals))]
    public void ClassicDialectExpandsTheSameDigitsIntoPlainDecimals(double value, string expected)
        => Assert.Equal(expected, SnbtWriter.Write(new NbtDouble(value), SnbtOptions.v1_13));

    [Fact]
    public void EveryLiteralStillRoundTripsUnderBothDialects()
    {
        double[] values =
        [
            1e20, 1e-20, 1e300, -1e20, 1e7, 9999999d, 1e-3, 1e-4, 1.5e-7, 0d, -0d, 1d, 0.1d, 100d, 150d,
            12345678d, 1.2345678901234568e17, 1d / 3d, double.MaxValue, double.MinValue, double.Epsilon,
        ];

        foreach (double value in values)
        {
            foreach (SnbtOptions options in new[] { SnbtOptions.v1_21_5, SnbtOptions.v1_13 })
            {
                string text = SnbtWriter.Write(new NbtDouble(value), options);
                Assert.Equal(value, Assert.IsType<NbtDouble>(SnbtParser.Parse(text, options)).Value);
            }
        }

        float[] singleValues = [1e20f, 1e-20f, 1e7f, 1e-4f, 0.1f, 1f / 3f, float.MaxValue, float.Epsilon, -2.5e-9f];
        foreach (float value in singleValues)
        {
            foreach (SnbtOptions options in new[] { SnbtOptions.v1_21_5, SnbtOptions.v1_13 })
            {
                string text = SnbtWriter.Write(new NbtFloat(value), options);
                Assert.Equal(value, Assert.IsType<NbtFloat>(SnbtParser.Parse(text, options)).Value);
            }
        }
    }

    [Fact]
    public void DigitSelectionCanStillDifferFromJavaForExtremeSubnormals()
    {
        // The digits come from the runtime's shortest round-trippable form, which is not always the digits
        // Java's algorithm picks. For the smallest subnormals the two disagree: Java prints "4.9E-324" for
        // Double.MIN_VALUE and "1.4E-45" for Float.MIN_VALUE, while the runtime's shortest forms are "5E-324"
        // and "1E-45". Both are the same value, so this is the one remaining textual difference, and it is
        // recorded here rather than papered over.
        Assert.Equal("5.0E-324d", SnbtWriter.Write(new NbtDouble(double.Epsilon), SnbtOptions.v1_21_5));
        Assert.Equal("1.0E-45f", SnbtWriter.Write(new NbtFloat(float.Epsilon), SnbtOptions.v1_21_5));

        Assert.Equal(double.Epsilon, Assert.IsType<NbtDouble>(
            SnbtParser.Parse("4.9E-324d", SnbtOptions.v1_21_5)).Value);
        Assert.Equal(double.Epsilon, Assert.IsType<NbtDouble>(
            SnbtParser.Parse("5.0E-324d", SnbtOptions.v1_21_5)).Value);
    }
}

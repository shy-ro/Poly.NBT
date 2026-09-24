using Poly.NBT.Dom;

namespace Poly.NBT.Tests;

public sealed class EdgeCaseTests
{
    public static TheoryData<NbtOptions> Dialects => new()
    {
        NbtOptions.JavaEdition,
        NbtOptions.JavaNetworkEdition,
        NbtOptions.BedrockEdition,
        NbtOptions.BedrockNetworkEdition,
    };

    [Theory]
    [MemberData(nameof(Dialects))]
    public void IntegerExtremesRoundTrip(NbtOptions options)
    {
        NbtSerializer serializer = NbtSerializer.Create(options with { RootTagNaming = NbtRootTagNaming.Omitted });

        foreach (int value in new[] { int.MinValue, -65, -64, -1, 0, 63, 64, 127, 128, 8191, 8192, int.MaxValue })
        {
            Assert.Equal(value, serializer.DeserializeUsingReflection<int>(serializer.SerializeUsingReflection(value, "")));
        }

        foreach (long value in new[] { long.MinValue, -1L, 0L, 1L << 56, long.MaxValue })
        {
            Assert.Equal(value, serializer.DeserializeUsingReflection<long>(serializer.SerializeUsingReflection(value, "")));
        }
    }

    [Theory]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(65535)]
    public void FixedWidthStringUnsignedLengthBoundaryRoundTrips(int length)
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        string value = new('a', length);

        Assert.Equal(value, serializer.DeserializeUsingReflection<string>(serializer.SerializeUsingReflection(value, "")));
    }

    [Fact]
    public void FixedWidthStringOver65535BytesThrows()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection(new string('a', 65536), ""));
    }

    [Fact]
    public void LongArrayFallsBackToListWhenDialectDoesNotSupportIt()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockEdition with { RootTagNaming = NbtRootTagNaming.Omitted });
        Assert.Equal(NbtTagType.List, (NbtTagType)serializer.SerializeUsingReflection(new[] { 1L }, "")[0]);
        byte[] encoded = serializer.SerializeUsingReflection(new[] { 1L }, "");
        Assert.Equal(new[] { 1L }, serializer.DeserializeUsingReflection<long[]>(encoded));
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void DomAndTypedPrimitiveArraysAgreeOnTheWire(NbtOptions options)
    {
        // The DOM converter routes TAG_Int_Array and TAG_Long_Array through the same bulk path as the strongly
        // typed converter, so the two must still agree byte for byte: same byte order, same VarInt fallback,
        // same element order. The bulk path is what removes the per-element overhead.
        NbtSerializer serializer = NbtSerializer.Create(options);
        int[] ints = [1, -2, 0x7fffffff, int.MinValue];
        byte[] intDom = SerializeDom(serializer, new NbtIntArray(ints));

        Assert.Equal(serializer.SerializeUsingReflection(ints, ""), intDom);
        Assert.Equal(ints, Assert.IsType<NbtIntArray>(DeserializeDom(serializer, intDom)).Value);
        Assert.Equal(ints, serializer.DeserializeUsingReflection<int[]>(intDom));

        if (!options.SupportsLongArray) return;

        long[] longs = [1L, -2L, long.MaxValue, long.MinValue];
        byte[] longDom = SerializeDom(serializer, new NbtLongArray(longs));

        Assert.Equal(serializer.SerializeUsingReflection(longs, ""), longDom);
        Assert.Equal(longs, Assert.IsType<NbtLongArray>(DeserializeDom(serializer, longDom)).Value);
        Assert.Equal(longs, serializer.DeserializeUsingReflection<long[]>(longDom));
    }

    [Fact]
    public void HeterogeneousListIsRejected()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        NbtList value = new(new NbtInt(1), new NbtString("two"));

        Assert.Throws<InvalidDataException>(() => SerializeDom(serializer, value));
    }

    [Fact]
    public void NonEmptyEndListIsRejected()
    {
        byte[] document = [9, 0, 0, 0, 0, 1];
        Assert.Throws<InvalidDataException>(() => DeserializeDom(NbtSerializer.Create(NbtOptions.JavaNetworkEdition), document));
    }

    [Fact]
    public void UnknownTagTypeIsRejected()
    {
        Assert.Throws<FormatException>(() => DeserializeDom(NbtSerializer.Create(NbtOptions.JavaNetworkEdition), [0xff]));
    }

    [Fact]
    public void NegativeArrayAndListLengthsAreRejected()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Throws<FormatException>(() => DeserializeDom(serializer, [7, 0xff, 0xff, 0xff, 0xff]));
        Assert.Throws<FormatException>(() => DeserializeDom(serializer, [9, 1, 0xff, 0xff, 0xff, 0xff]));
    }

    [Fact]
    public void TruncatedPayloadsThrowEndOfStream()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Throws<EndOfStreamException>(() => serializer.DeserializeUsingReflection<int>([3, 0, 0, 0]));
        Assert.Throws<EndOfStreamException>(() => DeserializeDom(serializer, [8, 0, 2, (byte)'a']));
        Assert.Throws<EndOfStreamException>(() => DeserializeDom(serializer, [7, 0, 0, 0, 3, 1, 2]));
    }

    [Fact]
    public void TruncatedAndOverlongVarIntsAreRejected()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);

        Assert.Throws<EndOfStreamException>(() => serializer.DeserializeUsingReflection<int>([3, 0x80]));
        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<int>([3, 0x80, 0x80, 0x80, 0x80, 0x80]));
        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<long>([4, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80]));
    }

    [Fact]
    public void VarIntsWiderThanTheTargetAreRejectedAsBadData()
    {
        // Five groups carry 35 bits and ten carry 70, so both widths can be over-encoded by a hostile
        // document. That has to read as malformed data; the old fixed-width cast reported OverflowException,
        // which looks like a library fault, and the old unconditional shift dropped the extra bits silently.
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);

        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<int>([3, 0xff, 0xff, 0xff, 0xff, 0x7f]));
        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<long>(
            [4, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7f]));

        // The widest legal encoding of each width still decodes, to that width's minimum value.
        Assert.Equal(int.MinValue, serializer.DeserializeUsingReflection<int>([3, 0xff, 0xff, 0xff, 0xff, 0x0f]));
        Assert.Equal(long.MinValue, serializer.DeserializeUsingReflection<long>(
            [4, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01]));
    }

    [Fact]
    public void InvalidModifiedUtf8IsRejected()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<string>([8, 0, 1, 0]));
        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<string>([8, 0, 2, 0xc2, 0x20]));
        Assert.Throws<FormatException>(() => serializer.DeserializeUsingReflection<string>([8, 0, 2, 0xc1, 0xbf]));
    }

    [Fact]
    public void TrailingDataIsRejectedByBufferApi()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Throws<InvalidDataException>(() => serializer.DeserializeUsingReflection<int>([3, 0, 0, 0, 1, 42]));
    }

    [Fact]
    public void PartialReadStreamIsSupported()
    {
        byte[] source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestFiles", "bigtest.nbt"));
        using var stream = new PartialReadStream(new MemoryStream(source, writable: false), maxRead: 1);

        NbtElement result = NbtSerializer.Create(NbtOptions.JavaEdition).Deserialize<NbtElement>(stream)!;

        Assert.IsType<NbtCompound>(result);
    }

    [Fact]
    public void UnknownCompoundPropertyIsSkippedRecursively()
    {
        NbtCompound document = new(
            new KeyValuePair<string, NbtElement>("Unknown", new NbtList(new NbtCompound(
                new KeyValuePair<string, NbtElement>("Nested", new NbtInt(1))))),
            new KeyValuePair<string, NbtElement>("Value", new NbtInt(42)));
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] bytes = SerializeDom(serializer, document);

        KnownProperty result = Assert.IsType<KnownProperty>(serializer.DeserializeUsingReflection<KnownProperty>(bytes));
        Assert.Equal(42, result.Value);
    }

    private static NbtElement DeserializeDom(NbtSerializer serializer, byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return serializer.Deserialize<NbtElement>(stream)!;
    }

    private static byte[] SerializeDom(NbtSerializer serializer, NbtElement value)
    {
        using var stream = new MemoryStream();
        serializer.Serialize<NbtElement>(stream, value, "");
        return stream.ToArray();
    }

    private sealed class PartialReadStream(Stream inner, int maxRead) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, maxRead));
        public override int Read(Span<byte> buffer) => inner.Read(buffer[..Math.Min(buffer.Length, maxRead)]);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public sealed class KnownProperty
{
    public int Value { get; set; }
}

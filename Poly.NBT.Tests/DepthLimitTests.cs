using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

/// <summary>
/// The readers and writers recurse once per nesting level, and the level count comes straight from the input.
/// Without a bound a few kilobytes of nested tags kill the process with an uncatchable stack overflow, so these
/// tests assert that the limits convert that into an ordinary exception.
/// </summary>
public sealed class DepthLimitTests
{
    private const int Limit = NbtOptions.DefaultMaxDepth;

    [Fact]
    public void EveryPresetCarriesTheDefaultLimit()
    {
        Assert.Equal(512, Limit);
        Assert.Equal(Limit, NbtOptions.JavaEdition.MaxDepth);
        Assert.Equal(Limit, NbtOptions.JavaNetworkEdition.MaxDepth);
        Assert.Equal(Limit, NbtOptions.BedrockEdition.MaxDepth);
        Assert.Equal(Limit, NbtOptions.BedrockNetworkEdition.MaxDepth);
        Assert.Equal(Limit, SnbtOptions.v1_13.MaxDepth);
        Assert.Equal(Limit, SnbtOptions.v1_21_5.MaxDepth);
    }

    [Fact]
    public void ReadingDeeplyNestedListsFailsInsteadOfOverflowingTheStack()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        // One level below the limit still parses.
        Assert.IsType<NbtList>(serializer.DeserializeUsingReflection<NbtElement>(NestedLists(Limit - 1)));

        // 20,000 levels used to kill the process here. It is only 100 KB of input.
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => serializer.DeserializeUsingReflection<NbtElement>(NestedLists(20_000)));
        Assert.Contains("NbtOptions.MaxDepth", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingDeclarativelyDeepTypesIsAlsoBounded()
    {
        // A Dictionary<string, object> nests through the runtime-object converter, whose recursion is driven by
        // the tag types on the wire rather than by the static type.
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] payload = CompoundChain(20_000);

        Assert.Throws<InvalidDataException>(
            () => serializer.DeserializeUsingReflection<Dictionary<string, object>>(payload));
    }

    [Fact]
    public void WritingDeeplyNestedListsFailsInsteadOfOverflowingTheStack()
    {
        NbtElement deep = DeepList(20_000);
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => serializer.Serialize(stream, deep, ""));
        Assert.Throws<InvalidDataException>(() => SnbtWriter.Write(deep));
    }

    [Fact]
    public void ParsingDeeplyNestedSnbtFailsWithAnOffset()
    {
        string text = new string('[', 20_000) + "1" + new string(']', 20_000);

        SnbtParseException error = Assert.Throws<SnbtParseException>(() => SnbtParser.Parse(text));
        Assert.InRange(error.Offset, Limit, Limit + 1);
        Assert.Contains("SnbtOptions.MaxDepth", error.Message, StringComparison.Ordinal);

        // Just inside the limit the same shape parses and round-trips.
        string accepted = new string('[', Limit - 2) + "1" + new string(']', Limit - 2);
        Assert.Equal(accepted, SnbtWriter.Write(SnbtParser.Parse(accepted)));
    }

    [Fact]
    public void TheLimitIsConfigurableOnBothOptionsTypes()
    {
        NbtSerializer shallow = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxDepth = 4 });
        Assert.IsType<NbtList>(shallow.DeserializeUsingReflection<NbtElement>(NestedLists(3)));
        Assert.Throws<InvalidDataException>(() => shallow.DeserializeUsingReflection<NbtElement>(NestedLists(4)));

        // Zero means "use the library default", not "forbid nesting".
        NbtSerializer defaulted = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxDepth = 0 });
        Assert.IsType<NbtList>(defaulted.DeserializeUsingReflection<NbtElement>(NestedLists(Limit - 1)));

        // Every value counts towards the limit, the innermost scalar included, so three nested lists plus
        // their scalar is exactly four levels.
        SnbtOptions shallowText = SnbtOptions.v1_21_5 with { MaxDepth = 4 };
        Assert.NotNull(SnbtParser.Parse("[[[1]]]", shallowText));
        Assert.Throws<SnbtParseException>(() => SnbtParser.Parse("[[[[1]]]]", shallowText));
    }

    [Fact]
    public void ANegativeLimitIsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxDepth = -1 }));
    }

    /// <summary>
    /// Builds a payload whose root is a <c>TAG_List</c> holding a single <c>TAG_List</c>, and so on, closed by
    /// an empty list. The result reaches <paramref name="depth"/> + 1 nesting levels.
    /// </summary>
    private static byte[] NestedLists(int depth)
    {
        byte[] payload = new byte[5 * (depth + 1) + 1];
        int offset = 0;
        payload[offset++] = (byte)NbtTagType.List;
        for (int index = 0; index < depth; index++)
        {
            payload[offset++] = (byte)NbtTagType.List;
            payload[offset++] = 0;
            payload[offset++] = 0;
            payload[offset++] = 0;
            payload[offset++] = 1;
        }

        payload[offset++] = (byte)NbtTagType.List;
        payload[offset++] = 0;
        payload[offset++] = 0;
        payload[offset++] = 0;
        payload[offset] = 0;
        return payload;
    }

    /// <summary>Builds a compound chain: <c>{a:{a:{...}}}</c>, closed by an empty compound.</summary>
    private static byte[] CompoundChain(int depth)
    {
        using var stream = new MemoryStream();
        stream.WriteByte((byte)NbtTagType.Compound);
        for (int index = 0; index < depth; index++)
        {
            stream.WriteByte((byte)NbtTagType.Compound);
            stream.WriteByte(0);
            stream.WriteByte(1);
            stream.WriteByte((byte)'a');
        }

        stream.WriteByte((byte)NbtTagType.End);
        return stream.ToArray();
    }

    private static NbtElement DeepList(int depth)
    {
        NbtElement element = new NbtInt(0);
        for (int index = 0; index < depth; index++) element = new NbtList(element);
        return element;
    }
}

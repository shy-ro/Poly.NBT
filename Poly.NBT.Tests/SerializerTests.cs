using Poly.NBT.Dom;
using PolyType;

namespace Poly.NBT.Tests;

public sealed class SerializerTests
{
    [Fact]
    public void JavaCompoundHasExpectedBytes()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        byte[] actual = serializer.SerializeUsingReflection(new Player(20, "Alex"), "");
        byte[] expected =
        [
            10,
            0, 0, // the empty root name: the field is part of the header, not optional
            3, 0, 6, (byte)'H', (byte)'e', (byte)'a', (byte)'l', (byte)'t', (byte)'h', 0, 0, 0, 20,
            8, 0, 4, (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0, 4, (byte)'A', (byte)'l', (byte)'e', (byte)'x',
            0,
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AnEmptyRootNameIsWrittenAsAnEmptyNameField()
    {
        // Java's NbtIo reads the name field unconditionally, so skipping it for an empty name produced a
        // document nothing but this library could read back.
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);

        byte[] intBytes = serializer.SerializeUsingReflection(42, "");
        Assert.Equal(new byte[] { 3, 0, 0, 0, 0, 0, 42 }, intBytes);
        Assert.Equal(42, serializer.DeserializeUsingReflection<int>(intBytes));

        Assert.Equal(new Player(20, "Alex"), serializer.DeserializeUsingReflection<Player>(
            serializer.SerializeUsingReflection(new Player(20, "Alex"), "")));
        Assert.Equal("", serializer.DeserializeDocument(
            serializer.SerializeUsingReflection(new Player(20, "Alex"), "")).RootTagName);
    }

    [Fact]
    public void AnOmittedRootNamingWritesNoNameFieldWhateverTheArgumentSays()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Equal(new byte[] { 3, 0, 0, 0, 42 }, serializer.SerializeUsingReflection(42, ""));
        Assert.Equal(new byte[] { 3, 0, 0, 0, 42 }, serializer.SerializeUsingReflection(42, "ignored"));
    }

    [Fact]
    public void JavaObjectRoundTrips()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        byte[] bytes = serializer.SerializeUsingReflection(new Player(20, "Alex"), "root");

        Assert.Equal(new Player(20, "Alex"), serializer.DeserializeUsingReflection<Player>(bytes));
    }

    [Fact]
    public void BedrockNetworkUsesZigZagVarInt()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);
        byte[] bytes = serializer.SerializeUsingReflection(300, "ignored");

        Assert.Equal(new byte[] { 3, 0xd8, 0x04 }, bytes);
        Assert.Equal(300, serializer.DeserializeUsingReflection<int>(bytes));
    }

    [Fact]
    public void DomNestedRoundTrips()
    {
        NbtElement value = new NbtCompound(
            new KeyValuePair<string, NbtElement>("name", new NbtString("world")),
            new KeyValuePair<string, NbtElement>("position", new NbtList(new NbtInt(1), new NbtInt(2), new NbtInt(3))));
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        using var stream = new MemoryStream();

        serializer.Serialize<NbtElement>(stream, value, "");
        stream.Position = 0;

        Assert.Equal(value, serializer.Deserialize<NbtElement>(stream));
    }

    [Fact]
    public void EmptyListUsesEndElementType()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        using var stream = new MemoryStream();

        serializer.Serialize<NbtElement>(stream, new NbtList(), "");

        Assert.Equal(new byte[] { 9, 0, 0, 0, 0, 0 }, stream.ToArray());
    }

    [Fact]
    public void RuntimeObjectListAcceptsDifferentCompoundShapes()
    {
        var value = new List<object> { new Position(1), new Named("two") };
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] bytes = serializer.SerializeUsingReflection(value, "");

        Assert.Equal(NbtTagType.List, (NbtTagType)bytes[0]);
        Assert.Equal(NbtTagType.Compound, (NbtTagType)bytes[1]);
        List<object> result = Assert.IsType<List<object>>(serializer.DeserializeUsingReflection<List<object>>(bytes));
        Assert.All(result, item => Assert.IsType<NbtCompound>(item));
    }

    [Fact]
    public void ArraysUseSpecializedTags()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Equal(NbtTagType.ByteArray, TagOf(serializer.SerializeUsingReflection(new byte[] { 1 }, "")));
        Assert.Equal(NbtTagType.ByteArray, TagOf(serializer.SerializeUsingReflection(new sbyte[] { -1 }, "")));
        Assert.Equal(NbtTagType.IntArray, TagOf(serializer.SerializeUsingReflection(new[] { 1 }, "")));
        Assert.Equal(NbtTagType.LongArray, TagOf(serializer.SerializeUsingReflection(new[] { 1L }, "")));
        Assert.Equal(NbtTagType.List, TagOf(serializer.SerializeUsingReflection(new[] { 1f }, "")));
        Assert.Equal(NbtTagType.List, TagOf(serializer.SerializeUsingReflection(new[] { 1d }, "")));
    }

    [Fact]
    public void PresetsHaveExpectedWireOptions()
    {
        Assert.Equal(NbtEndianness.BigEndian, NbtOptions.JavaEdition.Endianness);
        Assert.Equal(NbtRootTagNaming.Omitted, NbtOptions.JavaNetworkEdition.RootTagNaming);
        Assert.Equal(NbtEndianness.LittleEndian, NbtOptions.BedrockEdition.Endianness);
        Assert.Equal(NbtNumericEncoding.VarIntZigZag, NbtOptions.BedrockNetworkEdition.NumericEncoding);
    }

    [Fact]
    public void JavaAndBedrockUseTheirExpectedStringEncodings()
    {
        const string value = "A\0\U0001f600";
        NbtSerializer javaSerializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] java = javaSerializer.SerializeUsingReflection(value, "");
        Assert.Equal(new byte[] { 8, 0, 9, 0x41, 0xc0, 0x80, 0xed, 0xa0, 0xbd, 0xed, 0xb8, 0x80 }, java);
        Assert.Equal(value, javaSerializer.DeserializeUsingReflection<string>(java));

        NbtSerializer bedrockSerializer = NbtSerializer.Create(NbtOptions.BedrockEdition with { RootTagNaming = NbtRootTagNaming.Omitted });
        Assert.Equal(new byte[] { 8, 6, 0, 0x41, 0, 0xf0, 0x9f, 0x98, 0x80 }, bedrockSerializer.SerializeUsingReflection(value, ""));
    }

    [Fact]
    public void BedrockNetworkFloatRemainsFixedWidth()
    {
        byte[] bytes = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition).SerializeUsingReflection(1f, "");
        Assert.Equal(new byte[] { 5, 0, 0, 0x80, 0x3f }, bytes);
    }

    [Fact]
    public void NestedListsMayHaveDifferentInnerElementTypes()
    {
        NbtElement value = new NbtList(
            new NbtList(new NbtInt(1)),
            new NbtList(new NbtString("one")));
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        using var stream = new MemoryStream();

        serializer.Serialize<NbtElement>(stream, value, "");
        stream.Position = 0;

        Assert.Equal(value, serializer.Deserialize<NbtElement>(stream));
    }

    [Fact]
    public void OptionalPropertyIsAbsentOrPresent()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] absent = serializer.SerializeUsingReflection(new OptionalModel(), "");
        Assert.Equal(new byte[] { 10, 0 }, absent);
        Assert.Null(serializer.DeserializeUsingReflection<OptionalModel>(absent)!.Value);

        byte[] present = serializer.SerializeUsingReflection(new OptionalModel { Value = 42 }, "");
        Assert.Equal(42, serializer.DeserializeUsingReflection<OptionalModel>(present)!.Value);
    }

    [Fact]
    public void StringDictionaryUsesCompound()
    {
        var value = new Dictionary<string, int> { ["answer"] = 42 };
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] bytes = serializer.SerializeUsingReflection(value, "");

        Dictionary<string, int> result = Assert.IsType<Dictionary<string, int>>(serializer.DeserializeUsingReflection<Dictionary<string, int>>(bytes));
        Assert.Equal(42, result["answer"]);
    }

    [Fact]
    public void SourceGeneratedModelRoundTrips()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        using var stream = new MemoryStream();

        serializer.Serialize(stream, new SourcePlayer(7), "");
        stream.Position = 0;

        Assert.Equal(new SourcePlayer(7), serializer.Deserialize<SourcePlayer>(stream));
    }

    [Fact]
    public void ConvenienceSerializationRequiresExplicitRootName()
    {
        var methods = typeof(NbtSerializer).GetMethods();
        var sourceGenerated = Assert.Single(methods, method =>
            method.Name == nameof(NbtSerializer.Serialize)
            && method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var first }, _, _]
            && first == typeof(Stream));
        var reflection = Assert.Single(methods, method =>
            method.Name == nameof(NbtSerializer.SerializeUsingReflection)
            && method.IsGenericMethodDefinition);

        Assert.False(sourceGenerated.GetParameters()[2].HasDefaultValue);
        Assert.False(reflection.GetParameters()[1].HasDefaultValue);
    }

    [Fact]
    public void EnumsUseUnderlyingIntegerTagAndBits()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        AssertEnum(serializer, ByteEnum.Value, NbtTagType.Byte, "01");
        AssertEnum(serializer, SByteEnum.Value, NbtTagType.Byte, "80");
        AssertEnum(serializer, ShortEnum.Value, NbtTagType.Short, "8000");
        AssertEnum(serializer, UShortEnum.Value, NbtTagType.Short, "FFFF");
        AssertEnum(serializer, IntEnum.Value, NbtTagType.Int, "80000000");
        AssertEnum(serializer, UIntEnum.Value, NbtTagType.Int, "FFFFFFFF");
        AssertEnum(serializer, LongEnum.Value, NbtTagType.Long, "8000000000000000");
        AssertEnum(serializer, ULongEnum.Value, NbtTagType.Long, "FFFFFFFFFFFFFFFF");
    }

    [Fact]
    public void SourceGeneratedEnumPropertyRoundTrips()
    {
        var value = new SourceEnumModel(UIntEnum.Value);
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        using var stream = new MemoryStream();
        serializer.Serialize(stream, value, "");
        stream.Position = 0;

        Assert.Equal(value, serializer.Deserialize<SourceEnumModel>(stream));
    }

    private static void AssertEnum<TEnum>(NbtSerializer serializer, TEnum value, NbtTagType tag, string payloadHex)
        where TEnum : struct, Enum
    {
        byte[] bytes = serializer.SerializeUsingReflection(value, "");
        Assert.Equal([(byte)tag, .. Convert.FromHexString(payloadHex)], bytes);
        Assert.Equal(value, serializer.DeserializeUsingReflection<TEnum>(bytes));
    }

    private static NbtTagType TagOf(byte[] bytes) => (NbtTagType)bytes[0];
}

public sealed record Player(int Health, string Name);
public sealed record Position(int X);
public sealed record Named(string Name);

public sealed class OptionalModel
{
    public int? Value { get; set; }
}

[GenerateShape]
public sealed partial record SourcePlayer(int Score);

[GenerateShape]
public sealed partial record SourceEnumModel(UIntEnum Value);

public enum ByteEnum : byte { Value = 1 }
public enum SByteEnum : sbyte { Value = sbyte.MinValue }
public enum ShortEnum : short { Value = short.MinValue }
public enum UShortEnum : ushort { Value = ushort.MaxValue }
public enum IntEnum : int { Value = int.MinValue }
public enum UIntEnum : uint { Value = uint.MaxValue }
public enum LongEnum : long { Value = long.MinValue }
public enum ULongEnum : ulong { Value = ulong.MaxValue }

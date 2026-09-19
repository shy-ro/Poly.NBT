using System.Collections.Concurrent;
using System.Collections.Immutable;
using Poly.NBT.Dom;
using PolyType;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;

namespace Poly.NBT.Tests;

public sealed class GapCoverageTests
{
    [Fact]
    public void RootNamesAndTrailingDataFollowDocumentedContracts()
    {
        NbtSerializer java = NbtSerializer.Create(NbtOptions.JavaEdition);
        Assert.Equal(new byte[] { 3, 0, 0, 0, 1 }, java.SerializeUsingReflection(1, ""));
        Assert.Equal(new byte[] { 3, 0, 4, (byte)'r', (byte)'o', (byte)'o', (byte)'t', 0, 0, 0, 1 }, java.SerializeUsingReflection(1, "root"));
        NbtSerializer network = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Equal(network.SerializeUsingReflection(1, ""), network.SerializeUsingReflection(1, "anything"));
        using var stream = new MemoryStream(new byte[] { 3, 0, 0, 0, 1, 42 });
        Assert.Equal(1, network.Deserialize(stream, ReflectionTypeShapeProvider.Default.GetTypeShape<int>()));
        Assert.Equal(5, stream.Position);
        // Stream deserialization intentionally consumes one root and permits trailing data.
    }

    [Fact]
    public void FloatingArraysUseListTagsAndFixedBits()
    {
        Assert.Equal(new byte[] { 9, 5, 0, 0, 0, 2, 0x3f, 0x80, 0, 0, 0xbf, 0x80, 0, 0 }, NbtSerializer.Create(NbtOptions.JavaNetworkEdition).SerializeUsingReflection(new[] { 1f, -1f }, ""));
        Assert.Equal(new byte[] { 9, 6, 2, 0, 0, 0, 0, 0, 0, 0xf0, 0x3f }, NbtSerializer.Create(NbtOptions.BedrockNetworkEdition).SerializeUsingReflection(new[] { 1d }, ""));
    }

    [Fact]
    public void NestedListsAndCollectionMaterializationHaveStableBytes()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Equal(new byte[] { 9, 11, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 2 }, serializer.SerializeUsingReflection(new List<List<int>> { new() { 1 }, new() { 2 } }, ""));
        Assert.Equal(new byte[] { 9, 9, 0, 0, 0, 1, 11, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 }, serializer.SerializeUsingReflection(new List<List<List<int>>> { new() { new() { 1 } } }, ""));
        // Per NBT spec, an empty TAG_List uses TAG_End (0) as its element type.
        Assert.Equal(new byte[] { 9, 0, 0, 0, 0, 0 }, serializer.SerializeUsingReflection(new List<List<int>>(), ""));
        List<int> source = [1, 2, 3];
        byte[] expected = serializer.SerializeUsingReflection(source, "");
        Assert.Equal(expected, serializer.SerializeUsingReflection(source.ToArray(), ""));
        Assert.Equal(expected, serializer.SerializeUsingReflection((ICollection<int>)source, ""));
        Assert.Equal(expected, serializer.SerializeUsingReflection(Yield(source), ""));
    }

    [Fact]
    public void RuntimeObjectAndOptionalBoundariesRejectInvalidValues()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection(new List<object> { new NbtInt(1), new NbtString("a") }, ""));
        // NbtInt and int both produce TAG_Int, so they are homogeneous at the NBT level.
        List<object> mixedDomAndPrimitive = [new NbtInt(1), 42];
        byte[] mixedBytes = serializer.SerializeUsingReflection(mixedDomAndPrimitive, "");
        List<object> mixedResult = Assert.IsType<List<object>>(serializer.DeserializeUsingReflection<List<object>>(mixedBytes));
        Assert.Equal(2, mixedResult.Count);
        Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection((int?)null, ""));
        Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection(new List<int?> { null }, ""));
    }

    [Fact]
    public void DictionaryObjectEnumUnionAndSurrogateBoundaries()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] duplicate = [10, 0, 1, (byte)'a', 3, 0, 0, 0, 2, 0, 1, (byte)'a', 3, 0, 0, 0, 4, 0];
        Assert.Throws<InvalidDataException>(() => serializer.DeserializeUsingReflection<Dictionary<string, int>>(duplicate));
        Assert.Throws<NotSupportedException>(() => serializer.SerializeUsingReflection(new Dictionary<int, int> { [1] = 2 }, ""));
        var immutable = ImmutableDictionary<string, int>.Empty.Add("a", 1);
        Assert.Equal(immutable, serializer.DeserializeUsingReflection<ImmutableDictionary<string, int>>(serializer.SerializeUsingReflection(immutable, "")));
        Assert.Throws<InvalidDataException>(() => serializer.DeserializeUsingReflection<RequiredModel>([10, 0]));
        Assert.Throws<NotSupportedException>(() => serializer.SerializeUsingReflection(new object(), ""));
        Assert.Throws<NotSupportedException>(() => serializer.SerializeUsingReflection(TestEnum.One, ""));
        Assert.Throws<NotSupportedException>(() => serializer.SerializeUsingReflection<UnionBase>(new UnionChild(1), ""));
        var wrapped = new WrappedEnum(TestEnum.Two);
        Assert.Equal(wrapped, serializer.DeserializeUsingReflection<WrappedEnum>(serializer.SerializeUsingReflection(wrapped, "")));
        var custom = new CustomType(7);
        Assert.Equal(custom, serializer.DeserializeUsingReflection<CustomType>(serializer.SerializeUsingReflection(custom, "")));
    }

    [Fact]
    public void ConcurrentSerializerUseIsConsistent()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        ConcurrentBag<byte[]> values = [];
        Parallel.For(0, 100, i => values.Add(serializer.SerializeUsingReflection(new NormalRecord(i), "")));
        Assert.Equal(100, values.Count);
        Assert.All(values, bytes => Assert.IsType<NormalRecord>(serializer.DeserializeUsingReflection<NormalRecord>(bytes)));
    }

    [Fact]
    public void SkipPayloadCoversNestedTagsAndTruncation()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        NbtElement document = new NbtCompound(
            new("b", new NbtByte(1)), new("s", new NbtShort(2)), new("i", new NbtInt(3)), new("l", new NbtLong(4)),
            new("f", new NbtFloat(5)), new("d", new NbtDouble(6)), new("str", new NbtString("x")), new("ba", new NbtByteArray([1])),
            new("ia", new NbtIntArray([1])), new("la", new NbtLongArray([1])),
            new("nested", new NbtList(new NbtCompound(new KeyValuePair<string, NbtElement>("n", new NbtInt(1))))));
        using var output = new MemoryStream();
        serializer.Serialize<NbtElement>(output, document, "");
        byte[] bytes = output.ToArray();
        Assert.Equal(0, serializer.DeserializeUsingReflection<KnownProperty>(bytes)!.Value);
        Assert.Throws<EndOfStreamException>(() => serializer.DeserializeUsingReflection<KnownProperty>(bytes[..^1]));
        Assert.Throws<InvalidDataException>(() => serializer.DeserializeUsingReflection<KnownProperty>([10, 0, 0]));
    }

    [Fact]
    public void ReadOnlyWriteOnlyAndMaximumStringBoundaries()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        ReadOnlyModel result = serializer.DeserializeUsingReflection<ReadOnlyModel>(serializer.SerializeUsingReflection(new ReadOnlyModel(), "root"))!;
        Assert.Equal(0, result.Value); // Read-only members are skipped during deserialization.
        Assert.Equal(new byte[] { 10, 0 }, serializer.SerializeUsingReflection(new WriteOnlyModel { Value = 3 }, ""));
        byte[] bytes = serializer.SerializeUsingReflection(new string('a', 65535), "");
        Assert.Equal(new byte[] { 8, 0xff, 0xff }, bytes[..3]);
    }

    [Fact]
    public void ReflectionEntryPointsRoundTrip()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Equal(42, serializer.DeserializeUsingReflection<int>(serializer.SerializeUsingReflection(42, "")));
        // PublishAot requires a dedicated executable host; this covers the reflection API contract.
    }

    private static IEnumerable<int> Yield(IEnumerable<int> source) { foreach (int item in source) yield return item; }
}

public sealed record NormalRecord(int Value);
public sealed record RequiredModel(int X);
public sealed class ReadOnlyModel { public int Value { get; } }
public sealed class WriteOnlyModel { public int Value { set { } } }
public enum TestEnum { One, Two }
[DerivedTypeShape(typeof(UnionChild))] public abstract record UnionBase;
public sealed record UnionChild(int Value) : UnionBase;
[TypeShape(Marshaler = typeof(EnumMarshaler))] public readonly record struct WrappedEnum(TestEnum Value);
public sealed class EnumMarshaler : IMarshaler<WrappedEnum, int>
{
    public int Marshal(WrappedEnum value) => (int)value.Value;
    public WrappedEnum Unmarshal(int value) => new((TestEnum)value);
}
[TypeShape(Marshaler = typeof(CustomMarshaler))] public readonly record struct CustomType(int Value);
public sealed class CustomMarshaler : IMarshaler<CustomType, string>
{
    public string Marshal(CustomType value) => value.Value.ToString();
    public CustomType Unmarshal(string? value) => new(int.Parse(value!));
}

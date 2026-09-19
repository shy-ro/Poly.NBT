using Poly.NBT.Dom;
using PolyType.ReflectionProvider;

namespace Poly.NBT.Tests;

public sealed class NbtElementBridgeTests
{
    public static TheoryData<NbtOptions> Presets => new()
    {
        NbtOptions.JavaEdition,
        NbtOptions.JavaNetworkEdition,
        NbtOptions.BedrockEdition,
        NbtOptions.BedrockNetworkEdition,
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void ExplicitShapeBridgeRoundTripsAcrossPresets(NbtOptions options)
    {
        NbtSerializer serializer = NbtSerializer.Create(options);
        var shape = ReflectionTypeShapeProvider.Default.GetTypeShape<Player>();
        var value = new Player(20, "Alex");

        NbtElement element = serializer.ToElement(value, shape);

        Assert.Equal(
            new NbtCompound(
                new KeyValuePair<string, NbtElement>("Health", new NbtInt(20)),
                new KeyValuePair<string, NbtElement>("Name", new NbtString("Alex"))),
            element);
        Assert.Equal(value, serializer.FromElement(element, shape));
    }

    [Fact]
    public void SourceGeneratedBridgeRoundTrips()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        var value = new SourcePlayer(7);

        NbtElement element = serializer.ToElement(value);

        Assert.Equal(value, serializer.FromElement<SourcePlayer>(element));
    }

    [Fact]
    public void ReflectionBridgeRoundTrips()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);
        var value = new Player(20, "Alex");

        NbtElement element = serializer.ToElementUsingReflection(value);

        Assert.Equal(value, serializer.FromElementUsingReflection<Player>(element));
    }

    [Fact]
    public void BridgeRejectsNullAndIncompatibleValues()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        var stringShape = ReflectionTypeShapeProvider.Default.GetTypeShape<string>();
        var intShape = ReflectionTypeShapeProvider.Default.GetTypeShape<int>();

        Assert.Throws<InvalidDataException>(() => serializer.ToElement<string>(null, stringShape));
        Assert.Throws<ArgumentNullException>(() => serializer.FromElement<string>(null!, stringShape));
        Assert.Throws<ArgumentNullException>(() => serializer.FromElementUsingReflection<string>(null!));
        Assert.Throws<InvalidDataException>(() => serializer.FromElement(new NbtString("not an int"), intShape));
    }
}

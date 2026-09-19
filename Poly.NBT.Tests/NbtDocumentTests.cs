using Poly.NBT.Dom;

namespace Poly.NBT.Tests;

public sealed class NbtDocumentTests
{
    [Fact]
    public void NamedDocumentRoundTripsRootNameAndElement()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        var expected = new NbtDocument("level", new NbtCompound(new KeyValuePair<string, NbtElement>("value", new NbtInt(42))));

        byte[] bytes = serializer.Serialize(expected);
        NbtDocument actual = serializer.DeserializeDocument(bytes);

        Assert.Equal(expected, actual);
        using var stream = new MemoryStream();
        serializer.Serialize<NbtElement>(stream, expected.RootElement, expected.RootTagName);
        Assert.Equal(stream.ToArray(), bytes);
    }

    [Fact]
    public void OmittedRootPresetReturnsEmptyDocumentName()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] bytes = serializer.Serialize(new NbtDocument("not encoded", new NbtInt(1)));

        Assert.Equal(new NbtDocument("", new NbtInt(1)), serializer.DeserializeDocument(bytes));
    }

    [Fact]
    public void DocumentStreamConsumesOneRootWhileBufferRejectsTrailingData()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        byte[] root = serializer.Serialize(new NbtDocument("", new NbtInt(1)));
        byte[] withTrailingData = [.. root, 42];
        using var stream = new MemoryStream(withTrailingData);

        Assert.Equal(new NbtDocument("", new NbtInt(1)), serializer.DeserializeDocument(stream));
        Assert.Equal(root.Length, stream.Position);
        Assert.Throws<InvalidDataException>(() => serializer.DeserializeDocument(withTrailingData));
    }
}

using Poly.NBT.Dom;

namespace Poly.NBT.Tests;

public sealed class FnbtFixtureTests
{
    [Fact]
    public void SmallFixtureLoadsAndRoundTripsExactly()
    {
        byte[] source = File.ReadAllBytes(FixturePath("test.nbt"));
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        NbtCompound root = Assert.IsType<NbtCompound>(DeserializeDom(serializer, source));

        NbtString name = Assert.IsType<NbtString>(root["name"]);
        Assert.Equal("Bananrama", name.Value);

        Assert.Equal(source, SerializeDom(serializer, root, "hello world"));
    }

    [Fact]
    public void BigFixtureLoadsAllCoreTagKindsAndRoundTripsExactly()
    {
        byte[] source = File.ReadAllBytes(FixturePath("bigtest.nbt"));
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
        NbtCompound root = Assert.IsType<NbtCompound>(DeserializeDom(serializer, source));

        Assert.Equal(13, root.Count);
        Assert.Equal(long.MaxValue, Assert.IsType<NbtLong>(root["longTest"]).Value);
        Assert.Equal(short.MaxValue, Assert.IsType<NbtShort>(root["shortTest"]).Value);
        Assert.Equal(int.MaxValue, Assert.IsType<NbtInt>(root["intTest"]).Value);
        Assert.Equal("HELLO WORLD THIS IS A TEST STRING \u00c5\u00c4\u00d6!", Assert.IsType<NbtString>(root["stringTest"]).Value);
        Assert.Equal(0.49823147f, Assert.IsType<NbtFloat>(root["floatTest"]).Value);
        Assert.Equal(0.4931287132182315, Assert.IsType<NbtDouble>(root["doubleTest"]).Value);
        Assert.Equal(1000, Assert.IsType<NbtByteArray>(root[ByteArrayName]).Value.Length);
        Assert.Equal(10, Assert.IsType<NbtIntArray>(root["intArrayTest"]).Value.Length);
        Assert.Equal(5, Assert.IsType<NbtLongArray>(root["longArrayTest"]).Value.Length);

        NbtCompound nested = Assert.IsType<NbtCompound>(root["nested compound test"]);
        Assert.Equal("Hampus", Assert.IsType<NbtString>(Assert.IsType<NbtCompound>(nested["ham"])["name"]).Value);
        Assert.Equal("Eggbert", Assert.IsType<NbtString>(Assert.IsType<NbtCompound>(nested["egg"])["name"]).Value);

        NbtList longs = Assert.IsType<NbtList>(root["listTest (long)"]);
        Assert.Equal([11L, 12L, 13L, 14L, 15L], longs.Select(item => Assert.IsType<NbtLong>(item).Value));

        Assert.Equal(source, SerializeDom(serializer, root, "Level"));
    }

    private const string ByteArrayName =
        "byteArrayTest (the first 1000 values of (n*n*255+n*7)%100, starting with n=0 (0, 62, 34, 16, 8, ...))";

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    private static NbtElement DeserializeDom(NbtSerializer serializer, byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return serializer.Deserialize<NbtElement>(stream)!;
    }

    private static byte[] SerializeDom(NbtSerializer serializer, NbtElement value, string rootName)
    {
        using var stream = new MemoryStream();
        serializer.Serialize<NbtElement>(stream, value, rootName);
        return stream.ToArray();
    }
}

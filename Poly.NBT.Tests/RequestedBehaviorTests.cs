namespace Poly.NBT.Tests;

public sealed class RequestedBehaviorTests
{
    [Fact]
    public void PrimitiveListOptimizationCanBeDisabledAndOutputsInteroperate()
    {
        NbtSerializer optimized = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        NbtSerializer lists = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { OptimizePrimitiveListsToArrays = false });
        List<int> value = [1, -2];

        byte[] arrayBytes = optimized.SerializeUsingReflection(value);
        byte[] listBytes = lists.SerializeUsingReflection(value);
        Assert.Equal((byte)NbtTagType.IntArray, arrayBytes[0]);
        Assert.Equal(new byte[] { (byte)NbtTagType.List, (byte)NbtTagType.Int }, listBytes[..2]);
        Assert.Equal(value, optimized.DeserializeUsingReflection<List<int>>(listBytes));
        Assert.Equal(value, lists.DeserializeUsingReflection<List<int>>(arrayBytes));
        Assert.Equal(arrayBytes, optimized.SerializeUsingReflection(value.ToArray()));
        Assert.Equal((byte)NbtTagType.List, lists.SerializeUsingReflection(value.ToArray())[0]);
    }

    [Fact]
    public void NullablePrimitiveListRejectsNull()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection(new List<int?> { 1, null }));
    }

    [Fact]
    public void LongCollectionsFallBackWhenLongArrayIsUnsupported()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockEdition with { RootTagNaming = NbtRootTagNaming.Omitted });
        byte[] list = serializer.SerializeUsingReflection(new List<long> { 1 });
        byte[] array = serializer.SerializeUsingReflection(new long[] { 1 });
        Assert.Equal(new byte[] { (byte)NbtTagType.List, (byte)NbtTagType.Long }, list[..2]);
        Assert.Equal(new byte[] { (byte)NbtTagType.List, (byte)NbtTagType.Long }, array[..2]);
        Assert.Equal(new List<long> { 1 }, serializer.DeserializeUsingReflection<List<long>>(list));
        Assert.Equal(new long[] { 1 }, serializer.DeserializeUsingReflection<long[]>(array));
    }

    [Fact]
    public void BedrockUtf8EscapeCodecPreservesInvalidBytesSemantically()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockEdition with { RootTagNaming = NbtRootTagNaming.Omitted });
        string value = serializer.DeserializeUsingReflection<string>([8, 4, 0, 0x1b, (byte)'x', (byte)'F', (byte)'F'])!;
        Assert.Equal('\udcff', Assert.Single(value));
        Assert.Equal(new byte[] { 8, 4, 0, 0x1b, (byte)'x', (byte)'F', (byte)'F' }, serializer.SerializeUsingReflection(value));

        const string valid = "A\u4e2d\U0001f600";
        Assert.Equal(valid, serializer.DeserializeUsingReflection<string>(serializer.SerializeUsingReflection(valid)));
    }

    [Fact]
    public void Utf8EscapeParsingDistinguishesLiteralEscapeBoundaries()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockEdition with { RootTagNaming = NbtRootTagNaming.Omitted });
        Assert.Equal("\u001bq", serializer.DeserializeUsingReflection<string>([8, 2, 0, 0x1b, (byte)'q']));
        Assert.Equal("\u001b", serializer.DeserializeUsingReflection<string>([8, 1, 0, 0x1b]));
        Assert.Equal('\udcab', Assert.Single(serializer.DeserializeUsingReflection<string>([8, 4, 0, 0x1b, (byte)'X', (byte)'a', (byte)'b'])!));
    }
}

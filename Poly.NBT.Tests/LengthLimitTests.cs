using Poly.NBT.Dom;

namespace Poly.NBT.Tests;

/// <summary>
/// Every length prefix in the wire format drives an allocation before a single payload byte is read, so four
/// bytes of hostile input are enough to demand two gigabytes. These tests assert that
/// <see cref="NbtOptions.MaxCollectionLength"/> converts that into an ordinary
/// <see cref="InvalidDataException"/> instead of an <see cref="OutOfMemoryException"/>, and that the write side is
/// gated the same way.
/// </summary>
public sealed class LengthLimitTests
{
    private const int Limit = NbtOptions.DefaultMaxCollectionLength;

    /// <summary>A length the wire format can express but no real document needs.</summary>
    private const int AbsurdLength = int.MaxValue;

    [Fact]
    public void EveryPresetCarriesTheDefaultLimit()
    {
        Assert.Equal(1 << 24, Limit);
        Assert.Equal(Limit, NbtOptions.JavaEdition.MaxCollectionLength);
        Assert.Equal(Limit, NbtOptions.JavaNetworkEdition.MaxCollectionLength);
        Assert.Equal(Limit, NbtOptions.BedrockEdition.MaxCollectionLength);
        Assert.Equal(Limit, NbtOptions.BedrockNetworkEdition.MaxCollectionLength);
    }

    [Fact]
    public void AByteArrayLengthBombIsRejectedBeforeAllocating()
    {
        // TAG_Byte_Array, then a big-endian 0x7FFFFFFF. The old reader called new byte[int.MaxValue] first.
        byte[] payload = [(byte)NbtTagType.ByteArray, 0x7F, 0xFF, 0xFF, 0xFF];
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => serializer.DeserializeUsingReflection<byte[]>(payload));
        Assert.Contains("NbtOptions.MaxCollectionLength", error.Message, StringComparison.Ordinal);
        Assert.Contains(AbsurdLength.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AListLengthBombIsRejectedOnTheDomPathToo()
    {
        // TAG_List whose element type is TAG_Byte, with the same absurd count.
        byte[] payload = [(byte)NbtTagType.List, (byte)NbtTagType.Byte, 0x7F, 0xFF, 0xFF, 0xFF];
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Throws<InvalidDataException>(() => serializer.DeserializeUsingReflection<NbtElement>(payload));
    }

    [Fact]
    public void AnIntArrayLengthBombOnAVarIntDialectIsRejected()
    {
        // TAG_Int_Array on Bedrock's network format: the count is a VarInt-encoded ZigZag int, and
        // ZigZag(0x7FFFFFFF) encodes as FE FF FF FF 0F.
        byte[] payload = [(byte)NbtTagType.IntArray, 0xFE, 0xFF, 0xFF, 0xFF, 0x0F];
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => serializer.DeserializeUsingReflection<int[]>(payload));
        Assert.Contains("NbtOptions.MaxCollectionLength", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStringLengthBombIsRejectedAsBadDataRatherThanOverflowing()
    {
        // A VarInt length is a 32-bit unsigned field, so uint.MaxValue is representable on the wire. It has to
        // be rejected as out-of-range data; casting it to int first would have thrown OverflowException.
        byte[] payload = [(byte)NbtTagType.String, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F];
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => serializer.DeserializeUsingReflection<string>(payload));
        Assert.Contains("NbtOptions.MaxCollectionLength", error.Message, StringComparison.Ordinal);
        Assert.Contains(uint.MaxValue.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATruncatedCollectionReportsEndOfStreamRatherThanTheLimit()
    {
        // Ten bytes are announced but only three follow. That is a short read, not an oversized document, and
        // the two must stay distinguishable: nothing here allocates more than the announced ten bytes.
        byte[] payload = [(byte)NbtTagType.ByteArray, 0, 0, 0, 10, 1, 2, 3];
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);

        Assert.Throws<EndOfStreamException>(() => serializer.DeserializeUsingReflection<byte[]>(payload));
    }

    [Fact]
    public void TheLimitIsConfigurableAndZeroSelectsTheDefault()
    {
        NbtSerializer shallow = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxCollectionLength = 4 });
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, shallow.DeserializeUsingReflection<byte[]>([(byte)NbtTagType.ByteArray, 0, 0, 0, 4, 1, 2, 3, 4]));
        Assert.Throws<InvalidDataException>(
            () => shallow.DeserializeUsingReflection<byte[]>([(byte)NbtTagType.ByteArray, 0, 0, 0, 5, 1, 2, 3, 4, 5]));

        // Zero means "use the library default", not "forbid collections".
        NbtSerializer defaulted = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxCollectionLength = 0 });
        byte[] withinDefault = new byte[200];
        Array.Fill(withinDefault, (byte)7);
        Assert.Equal(withinDefault, defaulted.DeserializeUsingReflection<byte[]>(
            defaulted.SerializeUsingReflection(withinDefault, "")));
    }

    [Fact]
    public void WritingACollectionPastTheLimitIsRejectedToo()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxCollectionLength = 4 });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => serializer.SerializeUsingReflection(new byte[5], ""));
        Assert.Contains("NbtOptions.MaxCollectionLength", error.Message, StringComparison.Ordinal);

        // The limit is inclusive on both ends: a collection exactly at the limit still round-trips.
        byte[] atLimit = [1, 2, 3, 4];
        Assert.Equal(atLimit, serializer.DeserializeUsingReflection<byte[]>(serializer.SerializeUsingReflection(atLimit, "")));
    }

    [Fact]
    public void ANegativeLimitIsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { MaxCollectionLength = -1 }));
    }
}

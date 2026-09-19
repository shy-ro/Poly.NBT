using Poly.NBT.Dom;

namespace Poly.NBT.Tests;

public sealed class DomHelperTests
{
    [Fact]
    public void ScalarListsConvertToPrimitiveArrays()
    {
        Assert.True(new NbtList(new NbtByte(-1), new NbtByte(1)).TryToArray(out byte[]? bytes));
        Assert.Equal(new byte[] { 255, 1 }, bytes);
        Assert.True(new NbtList(new NbtByte(-1)).TryToArray(out sbyte[]? signedBytes));
        Assert.Equal(new sbyte[] { -1 }, signedBytes);
        Assert.True(new NbtList(new NbtShort(-2)).TryToArray(out short[]? shorts));
        Assert.Equal(new short[] { -2 }, shorts);
        Assert.True(new NbtList(new NbtInt(3)).TryToArray(out int[]? ints));
        Assert.Equal(new[] { 3 }, ints);
        Assert.True(new NbtList(new NbtLong(4)).TryToArray(out long[]? longs));
        Assert.Equal(new[] { 4L }, longs);
        Assert.True(new NbtList(new NbtFloat(5)).TryToArray(out float[]? floats));
        Assert.Equal(new[] { 5F }, floats);
        Assert.True(new NbtList(new NbtDouble(6)).TryToArray(out double[]? doubles));
        Assert.Equal(new[] { 6D }, doubles);
        Assert.True(new NbtList(new NbtString("seven")).TryToArray(out string[]? strings));
        Assert.Equal(new[] { "seven" }, strings);
    }

    [Fact]
    public void TryToArrayRejectsMismatchedElementsAndAcceptsEmptyLists()
    {
        var mixed = new NbtList(new NbtInt(1), new NbtLong(2));
        Assert.False(mixed.TryToArray(out int[]? mismatched));
        Assert.Null(mismatched);

        Assert.True(new NbtList().TryToArray(out int[]? empty));
        Assert.Empty(empty);
    }
}

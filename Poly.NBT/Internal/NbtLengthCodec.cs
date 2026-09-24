namespace Poly.NBT.Internal;

/// <summary>
/// Reads and writes the length prefixes that introduce every NBT string and collection.
/// </summary>
/// <remarks>
/// <para>
/// Every value that passes through <see cref="ReadCollectionLength"/> or <see cref="ReadStringLength"/> comes
/// from the wire and immediately drives an allocation, so both are validated against
/// <see cref="NbtOptions.MaxCollectionLength"/> here - one gate, ahead of every allocation site.
/// </para>
/// <para>
/// The <c>*Core</c> members return <see cref="long"/> so that a VarInt dialect can hand over its full 32-bit
/// unsigned length without an intermediate cast that would throw <see cref="OverflowException"/> on hostile
/// input; <see cref="Validate"/> then rejects anything above the limit as an ordinary data error.
/// </para>
/// </remarks>
internal abstract class NbtLengthCodec(int maxLength)
{
    public int ReadCollectionLength(Stream stream) => Validate(ReadCollectionLengthCore(stream));
    public int ReadStringLength(Stream stream) => Validate(ReadStringLengthCore(stream));
    public void WriteCollectionLength(Stream stream, int length) => WriteCollectionLengthCore(stream, Validate(length));
    public void WriteStringLength(Stream stream, int length) => WriteStringLengthCore(stream, Validate(length));

    protected abstract long ReadCollectionLengthCore(Stream stream);
    protected abstract long ReadStringLengthCore(Stream stream);
    protected abstract void WriteCollectionLengthCore(Stream stream, int length);
    protected abstract void WriteStringLengthCore(Stream stream, int length);

    private int Validate(long length)
    {
        if (length < 0) throw new FormatException("NBT lengths cannot be negative.");
        return length <= maxLength
            ? (int)length
            : throw new InvalidDataException(
                $"An NBT length of {length} exceeds NbtOptions.MaxCollectionLength ({maxLength}).");
    }
}

internal sealed class FixedLengthCodec(NbtNumericCodec numeric, int maxLength) : NbtLengthCodec(maxLength)
{
    protected override long ReadCollectionLengthCore(Stream stream) => numeric.ReadInt32(stream);

    protected override long ReadStringLengthCore(Stream stream)
    {
        int high = stream.ReadByte();
        int low = stream.ReadByte();
        if ((high | low) < 0) throw new EndOfStreamException();
        return numeric is BigEndianNumericCodec ? (high << 8) | low : (low << 8) | high;
    }

    protected override void WriteCollectionLengthCore(Stream stream, int length) => numeric.WriteInt32(stream, length);

    protected override void WriteStringLengthCore(Stream stream, int length)
    {
        if (length > ushort.MaxValue) throw new InvalidDataException("An NBT string cannot exceed 65535 encoded bytes in a fixed-width dialect.");
        if (numeric is BigEndianNumericCodec)
        {
            stream.WriteByte((byte)(length >> 8));
            stream.WriteByte((byte)length);
        }
        else
        {
            stream.WriteByte((byte)length);
            stream.WriteByte((byte)(length >> 8));
        }
    }
}

internal sealed class VarIntLengthCodec(int maxLength) : NbtLengthCodec(maxLength)
{
    protected override long ReadCollectionLengthCore(Stream stream) => VarInt.ZigZagDecode32(VarInt.ReadUInt32(stream));
    protected override long ReadStringLengthCore(Stream stream) => VarInt.ReadUInt32(stream);
    protected override void WriteCollectionLengthCore(Stream stream, int length) => VarInt.WriteUInt32(stream, VarInt.ZigZagEncode(length));
    protected override void WriteStringLengthCore(Stream stream, int length) => VarInt.WriteUInt32(stream, (uint)length);
}

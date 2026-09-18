namespace Poly.NBT.Internal;

internal abstract class NbtLengthCodec
{
    public abstract int ReadCollectionLength(Stream stream);
    public abstract int ReadStringLength(Stream stream);
    public abstract void WriteCollectionLength(Stream stream, int length);
    public abstract void WriteStringLength(Stream stream, int length);

    protected static int Validate(int length) => length >= 0 ? length : throw new FormatException("NBT lengths cannot be negative.");
}

internal sealed class FixedLengthCodec(NbtNumericCodec numeric) : NbtLengthCodec
{
    public override int ReadCollectionLength(Stream stream) => Validate(numeric.ReadInt32(stream));
    public override int ReadStringLength(Stream stream)
    {
        int high = stream.ReadByte();
        int low = stream.ReadByte();
        if ((high | low) < 0) throw new EndOfStreamException();
        return numeric is BigEndianNumericCodec ? (high << 8) | low : (low << 8) | high;
    }

    public override void WriteCollectionLength(Stream stream, int length) => numeric.WriteInt32(stream, Validate(length));
    public override void WriteStringLength(Stream stream, int length)
    {
        Validate(length);
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

internal sealed class VarIntLengthCodec : NbtLengthCodec
{
    public override int ReadCollectionLength(Stream stream) => Validate(VarInt.ZigZagDecode32(VarInt.ReadUInt32(stream)));
    public override int ReadStringLength(Stream stream) => checked((int)VarInt.ReadUInt32(stream));
    public override void WriteCollectionLength(Stream stream, int length) => VarInt.WriteUInt32(stream, VarInt.ZigZagEncode(Validate(length)));
    public override void WriteStringLength(Stream stream, int length) => VarInt.WriteUInt32(stream, checked((uint)Validate(length)));
}

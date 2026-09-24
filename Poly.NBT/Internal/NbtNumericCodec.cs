using System.Buffers.Binary;

namespace Poly.NBT.Internal;

internal abstract class NbtNumericCodec
{
    public abstract short ReadInt16(Stream stream);
    public abstract int ReadInt32(Stream stream);
    public abstract long ReadInt64(Stream stream);
    public abstract float ReadSingle(Stream stream);
    public abstract double ReadDouble(Stream stream);
    public abstract void WriteInt16(Stream stream, short value);
    public abstract void WriteInt32(Stream stream, int value);
    public abstract void WriteInt64(Stream stream, long value);
    public abstract void WriteSingle(Stream stream, float value);
    public abstract void WriteDouble(Stream stream, double value);
}

internal sealed class BigEndianNumericCodec : NbtNumericCodec
{
    public override short ReadInt16(Stream stream) => StreamIO.ReadInt16BigEndian(stream);
    public override int ReadInt32(Stream stream) => StreamIO.ReadInt32BigEndian(stream);
    public override long ReadInt64(Stream stream) => StreamIO.ReadInt64BigEndian(stream);
    public override float ReadSingle(Stream stream) => BitConverter.Int32BitsToSingle(ReadInt32(stream));
    public override double ReadDouble(Stream stream) => BitConverter.Int64BitsToDouble(ReadInt64(stream));
    public override void WriteInt16(Stream stream, short value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt16BigEndian);
    public override void WriteInt32(Stream stream, int value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt32BigEndian);
    public override void WriteInt64(Stream stream, long value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt64BigEndian);
    public override void WriteSingle(Stream stream, float value) => WriteInt32(stream, BitConverter.SingleToInt32Bits(value));
    public override void WriteDouble(Stream stream, double value) => WriteInt64(stream, BitConverter.DoubleToInt64Bits(value));
}

internal class LittleEndianNumericCodec : NbtNumericCodec
{
    public override short ReadInt16(Stream stream) => StreamIO.ReadInt16LittleEndian(stream);
    public override int ReadInt32(Stream stream) => StreamIO.ReadInt32LittleEndian(stream);
    public override long ReadInt64(Stream stream) => StreamIO.ReadInt64LittleEndian(stream);
    public override float ReadSingle(Stream stream) => BitConverter.Int32BitsToSingle(ReadInt32(stream));
    public override double ReadDouble(Stream stream) => BitConverter.Int64BitsToDouble(ReadInt64(stream));
    public override void WriteInt16(Stream stream, short value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt16LittleEndian);
    public override void WriteInt32(Stream stream, int value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt32LittleEndian);
    public override void WriteInt64(Stream stream, long value) => StreamIO.Write(stream, value, BinaryPrimitives.WriteInt64LittleEndian);
    public override void WriteSingle(Stream stream, float value) => WriteInt32(stream, BitConverter.SingleToInt32Bits(value));
    public override void WriteDouble(Stream stream, double value) => WriteInt64(stream, BitConverter.DoubleToInt64Bits(value));
}

internal sealed class VarIntNumericCodec : LittleEndianNumericCodec
{
    public override int ReadInt32(Stream stream) => VarInt.ZigZagDecode32(VarInt.ReadUInt32(stream));
    public override long ReadInt64(Stream stream) => VarInt.ZigZagDecode64(VarInt.ReadUInt64(stream));
    public override float ReadSingle(Stream stream) => BitConverter.Int32BitsToSingle(StreamIO.ReadInt32LittleEndian(stream));
    public override double ReadDouble(Stream stream) => BitConverter.Int64BitsToDouble(StreamIO.ReadInt64LittleEndian(stream));
    public override void WriteInt32(Stream stream, int value) => VarInt.WriteUInt32(stream, VarInt.ZigZagEncode(value));
    public override void WriteInt64(Stream stream, long value) => VarInt.WriteUInt64(stream, VarInt.ZigZagEncode(value));
    public override void WriteSingle(Stream stream, float value) => StreamIO.Write(stream, BitConverter.SingleToInt32Bits(value), BinaryPrimitives.WriteInt32LittleEndian);
    public override void WriteDouble(Stream stream, double value) => StreamIO.Write(stream, BitConverter.DoubleToInt64Bits(value), BinaryPrimitives.WriteInt64LittleEndian);
}

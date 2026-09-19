using System.Runtime.CompilerServices;

namespace Poly.NBT.Serialization;

internal sealed class NbtEnumConverter<TEnum, TUnderlying>(NbtSerializer serializer) : NbtConverter<TEnum>
    where TEnum : struct, Enum
    where TUnderlying : struct
{
    public override NbtTagType TagType => Type.GetTypeCode(typeof(TUnderlying)) switch
    {
        TypeCode.Byte or TypeCode.SByte => NbtTagType.Byte,
        TypeCode.Int16 or TypeCode.UInt16 => NbtTagType.Short,
        TypeCode.Int32 or TypeCode.UInt32 => NbtTagType.Int,
        TypeCode.Int64 or TypeCode.UInt64 => NbtTagType.Long,
        _ => throw new NotSupportedException($"Unsupported enum underlying type {typeof(TUnderlying)}."),
    };

    public override TEnum ReadPayload(Stream stream)
    {
        TUnderlying underlying;
        if (typeof(TUnderlying) == typeof(byte))
        {
            byte value = NbtSerializer.ReadByte(stream);
            underlying = Unsafe.As<byte, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(sbyte))
        {
            sbyte value = unchecked((sbyte)NbtSerializer.ReadByte(stream));
            underlying = Unsafe.As<sbyte, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(short))
        {
            short value = serializer.Numeric.ReadInt16(stream);
            underlying = Unsafe.As<short, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(ushort))
        {
            ushort value = unchecked((ushort)serializer.Numeric.ReadInt16(stream));
            underlying = Unsafe.As<ushort, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(int))
        {
            int value = serializer.Numeric.ReadInt32(stream);
            underlying = Unsafe.As<int, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(uint))
        {
            uint value = unchecked((uint)serializer.Numeric.ReadInt32(stream));
            underlying = Unsafe.As<uint, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(long))
        {
            long value = serializer.Numeric.ReadInt64(stream);
            underlying = Unsafe.As<long, TUnderlying>(ref value);
        }
        else if (typeof(TUnderlying) == typeof(ulong))
        {
            ulong value = unchecked((ulong)serializer.Numeric.ReadInt64(stream));
            underlying = Unsafe.As<ulong, TUnderlying>(ref value);
        }
        else
        {
            throw new NotSupportedException($"Unsupported enum underlying type {typeof(TUnderlying)}.");
        }

        return Unsafe.As<TUnderlying, TEnum>(ref underlying);
    }

    public override void WritePayload(Stream stream, TEnum value)
    {
        TUnderlying underlying = Unsafe.As<TEnum, TUnderlying>(ref value);
        if (typeof(TUnderlying) == typeof(byte)) stream.WriteByte(Unsafe.As<TUnderlying, byte>(ref underlying));
        else if (typeof(TUnderlying) == typeof(sbyte)) stream.WriteByte(unchecked((byte)Unsafe.As<TUnderlying, sbyte>(ref underlying)));
        else if (typeof(TUnderlying) == typeof(short)) serializer.Numeric.WriteInt16(stream, Unsafe.As<TUnderlying, short>(ref underlying));
        else if (typeof(TUnderlying) == typeof(ushort)) serializer.Numeric.WriteInt16(stream, unchecked((short)Unsafe.As<TUnderlying, ushort>(ref underlying)));
        else if (typeof(TUnderlying) == typeof(int)) serializer.Numeric.WriteInt32(stream, Unsafe.As<TUnderlying, int>(ref underlying));
        else if (typeof(TUnderlying) == typeof(uint)) serializer.Numeric.WriteInt32(stream, unchecked((int)Unsafe.As<TUnderlying, uint>(ref underlying)));
        else if (typeof(TUnderlying) == typeof(long)) serializer.Numeric.WriteInt64(stream, Unsafe.As<TUnderlying, long>(ref underlying));
        else if (typeof(TUnderlying) == typeof(ulong)) serializer.Numeric.WriteInt64(stream, unchecked((long)Unsafe.As<TUnderlying, ulong>(ref underlying)));
        else throw new NotSupportedException($"Unsupported enum underlying type {typeof(TUnderlying)}.");
    }
}

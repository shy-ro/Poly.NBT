using System.Diagnostics.CodeAnalysis;
using Poly.NBT.Dom;
using PolyType;
using PolyType.ReflectionProvider;

namespace Poly.NBT;

/// <summary>Provides bidirectional conversion between .NET object graphs and the NBT DOM.</summary>
/// <remarks>
/// <para>
/// Both directions go through the wire format: the value is serialized into a <see cref="MemoryStream"/> and
/// read back as an <see cref="NbtElement"/>, or the reverse. A conversion therefore costs a full serialize plus
/// a full deserialize and one intermediate byte buffer. That is deliberate - it keeps a single traversal
/// implementation instead of two that have to be kept in step - but it means these are convenience entry
/// points, not cheap conversions. With a stream-already-in-hand workflow,
/// <c>Serialize(Stream, T?, string, ITypeShape&lt;T&gt;)</c> writes the same bytes without the second pass.
/// </para>
/// <para>
/// A conversion is also subject to the same input limits as any other read: a graph deeper than
/// <see cref="NbtOptions.MaxDepth"/> or holding a collection larger than
/// <see cref="NbtOptions.MaxCollectionLength"/> fails with <see cref="InvalidDataException"/>.
/// </para>
/// </remarks>
public static class NbtElementExtensions
{
    extension(NbtSerializer serializer)
    {
        /// <summary>Converts a value to an <see cref="NbtElement"/> tree.</summary>
        /// <remarks>Serializes to a temporary stream and reads it back; see the type remarks.</remarks>
        public NbtElement ToElement<T>(T? value, ITypeShape<T> shape)
        {
            ArgumentNullException.ThrowIfNull(shape);
            return serializer.ToElementInternal(value, shape);
        }

        /// <summary>Reconstructs a value from an <see cref="NbtElement"/> tree.</summary>
        /// <remarks>Writes to a temporary stream and reads it back; see the type remarks.</remarks>
        public T? FromElement<T>(NbtElement element, ITypeShape<T> shape)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(shape);
            return serializer.FromElementInternal(element, shape);
        }

        /// <summary>Convenience overload for <see cref="IShapeable{T}"/> types.</summary>
        public NbtElement ToElement<T>(T? value) where T : IShapeable<T>
            => serializer.ToElementInternal(value, T.GetTypeShape());

        /// <summary>Convenience overload for <see cref="IShapeable{T}"/> types.</summary>
        public T? FromElement<T>(NbtElement element) where T : IShapeable<T>
            => serializer.FromElementInternal(element, T.GetTypeShape());

        /// <summary>Reflection-based overload. Requires dynamic code; not AOT-safe.</summary>
        [RequiresUnreferencedCode("The PolyType reflection provider requires unreferenced code.")]
        [RequiresDynamicCode("The PolyType reflection provider requires dynamic code.")]
        public NbtElement ToElementUsingReflection<T>(T? value)
            => serializer.ToElementInternal(value, ReflectionTypeShapeProvider.Default.GetTypeShape<T>());

        /// <summary>Reflection-based overload. Requires dynamic code; not AOT-safe.</summary>
        [RequiresUnreferencedCode("The PolyType reflection provider requires unreferenced code.")]
        [RequiresDynamicCode("The PolyType reflection provider requires dynamic code.")]
        public T? FromElementUsingReflection<T>(NbtElement element)
            => serializer.FromElementInternal(element, ReflectionTypeShapeProvider.Default.GetTypeShape<T>());
    }
}

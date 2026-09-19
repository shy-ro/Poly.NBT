using System.Diagnostics.CodeAnalysis;
using Poly.NBT.Dom;
using PolyType;
using PolyType.ReflectionProvider;

namespace Poly.NBT;

/// <summary>Provides bidirectional conversion between .NET object graphs and the NBT DOM.</summary>
public static class NbtElementExtensions
{
    extension(NbtSerializer serializer)
    {
        /// <summary>Converts a value to an <see cref="NbtElement"/> tree.</summary>
        public NbtElement ToElement<T>(T? value, ITypeShape<T> shape)
        {
            ArgumentNullException.ThrowIfNull(shape);
            return serializer.ToElementInternal(value, shape);
        }

        /// <summary>Reconstructs a value from an <see cref="NbtElement"/> tree.</summary>
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

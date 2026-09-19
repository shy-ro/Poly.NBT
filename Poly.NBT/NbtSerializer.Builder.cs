using Poly.NBT.Serialization;
using PolyType;
using PolyType.Abstractions;

namespace Poly.NBT;

public sealed partial class NbtSerializer
{
    private sealed class Builder(ITypeShapeFunc self, NbtSerializer configuredSerializer) : TypeShapeVisitor, ITypeShapeFunc
    {
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            if (serializer._builtIns.TryGetValue(typeof(T), out NbtConverter? converter)) return (NbtConverter<T>)converter;
            return shape.Accept(this, serializer);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            if (typeof(T) == typeof(object))
                return (NbtConverter<T>)(object)new RuntimeObjectConverter(serializer, shape.Provider);

            NbtPropertyConverter<T>[] properties = shape.Properties
                .Select(property => (NbtPropertyConverter<T>)property.Accept(this, serializer)!)
                .ToArray();
            return shape.Constructor is { } constructor
                ? constructor.Accept(this, new ObjectState<T>(serializer, properties))
                : new NbtObjectConverter<T>(serializer, properties);
        }

        public override object? VisitProperty<TDeclaring, TProperty>(IPropertyShape<TDeclaring, TProperty> property, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            return new NbtPropertyConverter<TDeclaring, TProperty>(serializer, property, GetOrAdd(property.PropertyType, serializer));
        }

        public override object? VisitConstructor<T, TArgumentState>(IConstructorShape<T, TArgumentState> constructor, object? state)
        {
            var objectState = (ObjectState<T>)state!;
            if (constructor.Parameters is [])
            {
                return new NbtDefaultObjectConverter<T>(objectState.Serializer, objectState.Properties, constructor.GetDefaultConstructor());
            }

            NbtPropertyConverter<TArgumentState>[] parameters = constructor.Parameters
                .Select(parameter => (NbtPropertyConverter<TArgumentState>)parameter.Accept(this, objectState.Serializer)!)
                .ToArray();
            return new NbtParameterizedObjectConverter<T, TArgumentState>(
                objectState.Serializer,
                objectState.Properties,
                parameters,
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor());
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            return new NbtPropertyConverter<TArgumentState, TParameter>(serializer, parameter, GetOrAdd(parameter.ParameterType, serializer));
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            NbtConverter<TElement> element = GetOrAdd(shape.ElementType, serializer);
            Func<TEnumerable, IEnumerable<TElement>> getter = shape.GetGetEnumerable();
            bool optimize = serializer.Options.OptimizePrimitiveListsToArrays;
            return shape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => new NbtMutableEnumerableConverter<TEnumerable, TElement>(
                    serializer, element, getter, shape.GetDefaultConstructor(), shape.GetAppender(), optimize),
                CollectionConstructionStrategy.Parameterized => new NbtParameterizedEnumerableConverter<TEnumerable, TElement>(
                    serializer, element, getter, shape.GetParameterizedConstructor(), optimize),
                _ => new NbtEnumerableConverter<TEnumerable, TElement>(serializer, element, getter, optimize),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            if (typeof(TKey) != typeof(string))
                throw new NotSupportedException($"NBT compound keys must be strings; {typeof(TDictionary)} uses {typeof(TKey)}.");

            NbtConverter<TValue> valueConverter = GetOrAdd(shape.ValueType, serializer);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> originalGetter = shape.GetGetDictionary();
            IEnumerable<KeyValuePair<string, TValue>> Getter(TDictionary dictionary)
            {
                foreach (KeyValuePair<TKey, TValue> pair in originalGetter(dictionary))
                    yield return new((string)(object)pair.Key, pair.Value);
            }

            return shape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => CreateMutableDictionary(shape, serializer, valueConverter, Getter),
                CollectionConstructionStrategy.Parameterized => CreateParameterizedDictionary(shape, serializer, valueConverter, Getter),
                _ => new NbtDictionaryConverter<TDictionary, TValue>(serializer, valueConverter, Getter),
            };
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            return new NbtOptionalConverter<TOptional, TElement>(GetOrAdd(shape.ElementType, serializer), shape.GetDeconstructor(), shape.GetSomeConstructor());
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> shape, object? state)
        {
            NbtSerializer serializer = ResolveSerializer(state);
            return new NbtSurrogateConverter<T, TSurrogate>(shape.Marshaler, GetOrAdd(shape.SurrogateType, serializer));
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> shape, object? state)
            => new NbtEnumConverter<TEnum, TUnderlying>(ResolveSerializer(state));

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> shape, object? state)
            => throw new NotSupportedException("NBT union representation has not been selected. Use a PolyType surrogate to specify one.");

        private NbtConverter<T> GetOrAdd<T>(ITypeShape<T> shape, NbtSerializer serializer)
            => (NbtConverter<T>)self.Invoke(shape, serializer)!;

        private NbtSerializer ResolveSerializer(object? state)
            => state as NbtSerializer ?? configuredSerializer;

        private static NbtConverter<TDictionary> CreateMutableDictionary<TDictionary, TKey, TValue>(
            IDictionaryTypeShape<TDictionary, TKey, TValue> shape,
            NbtSerializer serializer,
            NbtConverter<TValue> valueConverter,
            Func<TDictionary, IEnumerable<KeyValuePair<string, TValue>>> getter)
            where TKey : notnull
        {
            MutableCollectionConstructor<TKey, TDictionary> constructor = shape.GetDefaultConstructor();
            DictionaryInserter<TDictionary, TKey, TValue> inserter = shape.GetInserter(DictionaryInsertionMode.Throw);
            TDictionary Constructor(in CollectionConstructionOptions<string> _) => constructor();
            bool Insert(ref TDictionary dictionary, string key, TValue value) => inserter(ref dictionary, (TKey)(object)key, value);
            return new NbtMutableDictionaryConverter<TDictionary, TValue>(serializer, valueConverter, getter, Constructor, Insert);
        }

        private static NbtConverter<TDictionary> CreateParameterizedDictionary<TDictionary, TKey, TValue>(
            IDictionaryTypeShape<TDictionary, TKey, TValue> shape,
            NbtSerializer serializer,
            NbtConverter<TValue> valueConverter,
            Func<TDictionary, IEnumerable<KeyValuePair<string, TValue>>> getter)
            where TKey : notnull
        {
            ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor = shape.GetParameterizedConstructor();
            TDictionary Constructor(ReadOnlySpan<KeyValuePair<string, TValue>> values, in CollectionConstructionOptions<string> _)
            {
                KeyValuePair<TKey, TValue>[] converted = new KeyValuePair<TKey, TValue>[values.Length];
                for (int index = 0; index < values.Length; index++)
                    converted[index] = new((TKey)(object)values[index].Key, values[index].Value);
                return constructor(converted);
            }
            return new NbtParameterizedDictionaryConverter<TDictionary, TValue>(serializer, valueConverter, getter, Constructor);
        }

        private sealed record ObjectState<T>(NbtSerializer Serializer, NbtPropertyConverter<T>[] Properties);
    }
}

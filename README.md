# Poly.NBT

PolyType-based, Native AOT-friendly serialization for the Java and Bedrock NBT wire formats.

## Implemented / Planned

| Area | Implemented | Planned |
|:---|:---|:---|
| Configuration | `NbtOptions` with `Endianness`, `StringEncoding`, `NumericEncoding`, `RootTagNaming`, `SupportsLongArray`, `OptimizePrimitiveListsToArrays` | — |
| Presets | `JavaEdition`, `JavaNetworkEdition`, `BedrockEdition`, `BedrockNetworkEdition` | — |
| Encoding | Big/little endian numbers, ZigZag VarInt, Modified UTF-8, strict UTF-8, UTF-8 with escapes, fixed and VarInt length prefixes | — |
| Converters | Primitives, arrays, collections, dictionaries, objects, `Nullable<T>`, surrogates, enums, DOM, `object` | — |
| DOM | `NbtElement` and 12 tag types, `NbtList.TryToArray`, `NbtDocument` | `ToElement` / `FromElement` bridge |
| API | `Serialize`, `Deserialize`, `SerializeUsingReflection`, `DeserializeUsingReflection`, `DeserializeDocument` | Async API |
| Polymorphism | — | Union and derived-type serialization |
| BCL types | — | Built-in marshalers for `decimal`, `Guid`, `DateTime`, `DateTimeOffset` |

## Usage

### Basic

```csharp
NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);

byte[] bytes = serializer.Serialize(value, "root", MyModel.GetTypeShape());
MyModel? restored = serializer.Deserialize<MyModel>(bytes);
```

With `[GenerateShape]` on `MyModel`:

```csharp
NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);

serializer.Serialize(stream, value, "root");
MyModel? restored = serializer.Deserialize<MyModel>(stream);
```

### Configuration

```csharp
NbtSerializer java = NbtSerializer.Create(NbtOptions.JavaEdition);
NbtSerializer custom = NbtSerializer.Create(NbtOptions.JavaEdition with { SupportsLongArray = false });
NbtSerializer bedrock = NbtSerializer.Create(NbtOptions.BedrockNetworkEdition);
```

### Root name

```csharp
// Write with a root name
serializer.Serialize(stream, value, "level", shape);

// Write without a root name
serializer.Serialize(stream, value, "", shape);

// Read a stream that omitted the root name
MyModel? restored = serializer.Deserialize(stream, shape, rootNameOmitted: true);
```

### Document

```csharp
NbtDocument doc = serializer.DeserializeDocument(bytes);
NbtElement root = doc.RootElement;
string name = doc.RootTagName;
```

### DOM helpers

```csharp
NbtList list = new(new NbtInt(1), new NbtInt(2), new NbtInt(3));
if (list.TryToArray(out int[]? values))
{
    // values is [1, 2, 3]
}
```

Overloads: `byte[]`, `sbyte[]`, `short[]`, `int[]`, `long[]`, `float[]`, `double[]`, `string[]`.

### PolyType attributes

```csharp
[GenerateShape]
public partial record Player(int Health, string Name);

[GenerateShape]
public partial class Entity
{
    [PropertyShape(Name = "id")]
    public int Identifier { get; set; }

    [PropertyShape(Ignore = true)]
    public string CacheKey { get; set; } = "";
}

[GenerateShape]
[TypeShape(Marshaler = typeof(PointMarshaler))]
public readonly partial record struct Point(int X, int Y);
```

See the [PolyType documentation](https://github.com/eiriktsarpalis/PolyType) for the full attribute surface.

## Limitations

- **`Utf8WithEscapes` literal escape ambiguity.** If a string contains the literal sequence `ESC x HH` (a `U+001B` character followed by `x` and two hex digits), the encoded form loses the `x` and hex digits on round-trip. This is a rare boundary; the fix would significantly increase complexity and is not planned.
- **Asymmetric `long[]` deserialization.** When `OptimizePrimitiveListsToArrays` is `false`, `long[]` and `List<long>` still read `TAG_Long_Array` input. Deserialization is driven by the actual tag on the wire and is not constrained by the serializer's output configuration.

## Requirements

- **Root values cannot be `null`.** `Serialize` throws `InvalidDataException` if the root value is absent.
- **List elements cannot be `null`.** NBT has no null representation. `null` elements throw `InvalidDataException` at write time, regardless of whether `OptimizePrimitiveListsToArrays` is enabled.
- **Dictionary keys must be `string`.** Any other key type throws `NotSupportedException` during converter construction.
- **Enums map to the smallest integer tag for their underlying type.** `byte`/`sbyte` → `TAG_Byte`, `short`/`ushort` → `TAG_Short`, `int`/`uint` → `TAG_Int`, `long`/`ulong` → `TAG_Long`. No string-based names are emitted.
- **`NbtSerializer.Create` requires explicit options.** There is no parameterless overload.
- **AOT compatibility.** The library is marked `IsAotCompatible`. The reflection-based `SerializeUsingReflection` / `DeserializeUsingReflection` methods require dynamic code and are annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.

## Acknowledgements

The fixtures `test.nbt` and `bigtest.nbt` in `Poly.NBT.Tests/TestFiles` originate from [fNbt](https://github.com/mstefarov/fNbt) and are retained under BSD-3-Clause in `Poly.NBT.Tests/TestFiles/fNbt-LICENSE.txt`.
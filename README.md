# Poly.NBT

PolyType-based, Native AOT-friendly serialization for the Java and Bedrock NBT wire formats.

The serializer is configured once and can be shared by concurrent callers:

```csharp
NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
serializer.Serialize(stream, value, "root", MyModel.GetTypeShape());
```

Serialization convenience overloads also require an explicit root name:

```csharp
byte[] data = serializer.SerializeUsingReflection(value, "root");
```

Passing an empty root name omits both the name length and name bytes. On reading that form with a named-root preset, pass `rootNameOmitted: true`. The Java and Bedrock network presets always omit the root name regardless of the supplied value.

## DOM documents and lists

`NbtDocument` keeps a root tag name and root element together. Document deserialization preserves names that are present on the wire; formats that omit the root name return `string.Empty`.

```csharp
var document = new NbtDocument("level", new NbtInt(42));
byte[] data = serializer.Serialize(document);
NbtDocument decoded = serializer.DeserializeDocument(data);
```

A homogeneous scalar `NbtList` can be converted without manually inspecting every element:

```csharp
var list = new NbtList(new NbtInt(1), new NbtInt(2));
if (list.TryToArray(out int[]? values))
{
    // values is [1, 2]
}
```

`TryToArray` overloads are available for `byte[]`, `sbyte[]`, `short[]`, `int[]`, `long[]`, `float[]`, `double[]`, and `string[]`. A mismatched element tag returns `false`; an empty list converts successfully to any explicitly selected target type.

## Wire-format decisions

- Java fixed-width numbers use big endian; Bedrock fixed-width numbers use little endian.
- Bedrock network `TAG_Int` and `TAG_Long` use ZigZag VarInt/VarLong. Floating-point tags remain fixed-width IEEE 754 values in little-endian byte order.
- Java strings use Java Modified UTF-8. Bedrock strings use tolerant UTF-8 with ESC x HH raw-byte escapes.

When deserializing into `object`, scalar tags are unpacked to their CLR primitive values; list and compound tags remain `NbtList` and `NbtCompound`.

Enums use the integer NBT tag matching the width of their CLR underlying type:

- `byte` and `sbyte` use `TAG_Byte`.
- `short` and `ushort` use `TAG_Short`.
- `int` and `uint` use `TAG_Int`.
- `long` and `ulong` use `TAG_Long`.
- Unsigned underlying values preserve their bit pattern in the corresponding signed NBT payload.
- Fixed-width string lengths are unsigned 16-bit values. Network string lengths are unsigned VarInts; network collection lengths are signed ZigZag VarInts.
- Empty lists are emitted with `TAG_End` as their element type.
- Unions require a PolyType surrogate until their NBT representation is selected by the application.

Run the dependency-free microbenchmarks with `dotnet run --project benchmarks/Poly.NBT.Benchmarks/Poly.NBT.Benchmarks.csproj -c Release`.

The format behavior follows the [NBT format specification](https://minecraft.wiki/w/NBT_format), the [Bedrock protocol data types](https://minecraft.wiki/w/Bedrock_Edition_protocol#Data_types), and the [Java protocol NBT change](https://minecraft.wiki/w/Java_Edition_protocol/Packets#NBT) introduced in 1.20.2. The visitor and recursive converter cache follow PolyType's [CborSerializer example](https://github.com/eiriktsarpalis/PolyType/tree/v1.3.1/src/PolyType.Examples/CborSerializer).

## Acknowledgements

The implementation and compatibility tests also reference [fNbt](https://github.com/mstefarov/fNbt). The uncompressed `test.nbt` and `bigtest.nbt` fixtures in `Poly.NBT.Tests/TestFiles` originate from fNbt, and several edge-case scenarios were independently rewritten from its test suite. The fixtures retain fNbt's BSD-3-Clause license in `Poly.NBT.Tests/TestFiles/fNbt-LICENSE.txt`.

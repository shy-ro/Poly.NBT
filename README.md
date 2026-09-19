# Poly.NBT

PolyType-based, Native AOT-friendly serialization for the Java and Bedrock NBT wire formats.

The serializer is configured once and can be shared by concurrent callers:

```csharp
NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);
serializer.Serialize(stream, value, "root", MyModel.GetTypeShape());
```

Passing an empty root name omits both the name length and name bytes. On reading that form with a named-root preset, pass `rootNameOmitted: true`. The Java network preset always omits the root name.

## Wire-format decisions

- Java fixed-width numbers use big endian; Bedrock fixed-width numbers use little endian.
- Bedrock network `TAG_Int` and `TAG_Long` use ZigZag VarInt/VarLong. Floating-point tags remain fixed-width IEEE 754 values in little-endian byte order.
- Java strings use Java Modified UTF-8. Bedrock strings use tolerant UTF-8 with ESC x HH raw-byte escapes.

When deserializing into `object`, scalar tags are unpacked to their CLR primitive values; list and compound tags remain `NbtList` and `NbtCompound`.

Future DOM conveniences may add explicit `NbtList` conversions to primitive arrays and helpers between `NbtCompound` and structured objects. They are intentionally outside the current wire serializer API.

Run the dependency-free microbenchmarks with `dotnet run --project benchmarks/Poly.NBT.Benchmarks/Poly.NBT.Benchmarks.csproj -c Release`.
- Fixed-width string lengths are unsigned 16-bit values. Network string lengths are unsigned VarInts; network collection lengths are signed ZigZag VarInts.
- Empty lists are emitted with `TAG_End` as their element type.
- Enums and unions deliberately require a PolyType surrogate until their NBT representation is selected by the application.

The format behavior follows the [NBT format specification](https://minecraft.wiki/w/NBT_format), the [Bedrock protocol data types](https://minecraft.wiki/w/Bedrock_Edition_protocol#Data_types), and the [Java protocol NBT change](https://minecraft.wiki/w/Java_Edition_protocol/Packets#NBT) introduced in 1.20.2. The visitor and recursive converter cache follow PolyType's [CborSerializer example](https://github.com/eiriktsarpalis/PolyType/tree/v1.3.1/src/PolyType.Examples/CborSerializer).

## Acknowledgements

The implementation and compatibility tests also reference [fNbt](https://github.com/mstefarov/fNbt). The uncompressed `test.nbt` and `bigtest.nbt` fixtures in `Poly.NBT.Tests/TestFiles` originate from fNbt, and several edge-case scenarios were independently rewritten from its test suite. The fixtures retain fNbt's BSD-3-Clause license in `Poly.NBT.Tests/TestFiles/fNbt-LICENSE.txt`.

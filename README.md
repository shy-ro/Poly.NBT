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
| SNBT | `SnbtParser`, `SnbtWriter`, `SnbtOptions` (`v1_13` and `v1_21_5` dialects) | — |
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

A named dialect always writes the name field, so an empty name is written as an empty name (`00 00`), not
omitted. Java's reader consumes that field unconditionally; leaving it out produced a document no Java
reader could parse. Whether the field exists at all is a property of the dialect, not of the call:

```csharp
NbtSerializer named = NbtSerializer.Create(NbtOptions.JavaEdition);          // TAG + name + payload
NbtSerializer network = NbtSerializer.Create(NbtOptions.JavaNetworkEdition); // RootTagNaming = Omitted

// Both of these write a two-byte empty name.
named.Serialize(stream, value, "", shape);
named.Serialize(stream, value, "level", shape);

// JavaNetworkEdition writes no name field at all, whatever the name argument says.
network.Serialize(stream, value, "level", shape);

// Reading a name-less stream with a named dialect is an explicit opt-in for non-standard input.
MyModel? restored = named.Deserialize(stream, shape, rootNameOmitted: true);
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

### Nesting depth

Both readers and writers recurse once per nesting level, and the level count comes from the input, so
the depth is bounded: `NbtOptions.MaxDepth` and `SnbtOptions.MaxDepth` default to 512, counting the
outermost value as level one. Past the limit the binary reader and both writers throw
`InvalidDataException`, and the SNBT parser throws `SnbtParseException` with the `Offset` of the
offending bracket — the message names the option to raise. Zero selects the library default.

The bound exists because `StackOverflowException` cannot be caught in .NET: without it, 15 KB of nested
`TAG_List` headers or 6 KB of nested brackets terminate the process, which no host can defend against.

### Collection and string lengths

A length prefix is four bytes (or a VarInt) that immediately drives an allocation, so a hostile document
can ask for two gigabytes before one payload byte has been read. `NbtOptions.MaxCollectionLength` caps
both the element count of a collection and the encoded byte length of a string; it applies to reading and
to writing, and defaults to `1 << 24` elements (zero selects the library default). Past the limit reads
and writes throw `InvalidDataException`, and a negative limit is rejected when the serializer is created.

The default admits every realistic document — a whole 16³ chunk section is a few thousand elements — while
keeping the worst case for a hostile `TAG_Long_Array` in the low hundreds of megabytes. A truncated
collection is still reported as `EndOfStreamException`, not as an oversized one, so the two conditions stay
distinguishable.

The limit is per collection. It does not bound a document's *total* size: a broad tree of individually
legal collections can still add up. When accepting NBT from an untrusted source, bound the input as well —
reject streams larger than a fixed number of bytes before handing them to `NbtSerializer`. Minecraft's
`NbtAccounter` solves the same problem with running total accounting; `MaxCollectionLength` is the
per-value equivalent.

### Malformed input

Three exception types divide the ways a document can be wrong, so a caller can tell bad bytes from bytes it
is not configured to accept:

| Exception | Meaning |
|:---|:---|
| `FormatException` | The bytes are structurally invalid: an unknown tag type, a negative length, a Modified UTF-8 sequence that is not well-formed, or a VarInt that does not fit its target width. |
| `InvalidDataException` | The bytes are structurally valid but break a rule — a configured limit such as `MaxDepth` or `MaxCollectionLength`, a heterogeneous `TAG_List`, a missing root value. |
| `EndOfStreamException` | The document is truncated. A short collection stays in this category rather than being reported as oversized. |

A VarInt encoding wider than its target is deliberately counted as malformed rather than allowed to
truncate: five groups encode 35 bits, so a hostile `TAG_Int` can carry a value that does not fit in 32, and
silently keeping the low bits would invent a value the sender never wrote.

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

## SNBT text

`Poly.NBT.Snbt` converts between the SNBT text format and the `NbtElement` DOM. It depends only on
`Poly.NBT.Dom`; it never touches the binary serializer or PolyType.

```csharp
NbtElement element = SnbtParser.Parse("{name:Bananrama,Health:20b,Pos:[1.0d,2.0d,3.0d]}");
string text = SnbtWriter.Write(element);
NbtDocument document = SnbtParser.ParseDocument(new StringReader(text));
```

`SnbtParser.Parse` reads exactly one value and throws `SnbtParseException` (carrying the character
`Offset` of the failure) on malformed input. The text overloads take a `ReadOnlySpan<char>`, so a `string`
binds to them through the built-in implicit conversion and no intermediate copy is created; the
`TextReader` overloads cover streams. SNBT has no root tag name, so `ParseDocument` returns a
document whose `RootTagName` is `string.Empty`. `SnbtWriter` emits compact single-line output:
byte/short/long literals carry their `b`/`s`/`L` suffixes, floating point uses `"R"` round-trip
formatting followed by `f`/`d`, and strings stay bare unless quoting is required. `SnbtWriter.Write(TextWriter, ...)`
streams the tree element by element and never materializes the whole document as a string; the `string`
overloads collect the same writes in a `StringWriter`. Every overload has a counterpart that takes an
`SnbtOptions` value; `SnbtWriter.Write(element, SnbtOptions.v1_13)` therefore produces text the classic
dialect can read back.

### Dialects

`SnbtOptions` selects the accepted grammar. Each flag mirrors a syntax extension introduced by
Minecraft 1.21.5; `v1_13` disables every one of them and `v1_21_5` enables every one of them.

| Option | v1_13 | v1_21_5 |
|:---|:---:|:---:|
| `AllowTrailingCommas` | No | Yes |
| `AllowHeterogeneousLists` | No | Yes |
| `AllowScientificNotation` | No | Yes |
| `AllowBinaryAndHexLiterals` | No | Yes |
| `AllowOmittedFloatParts` | No | Yes |
| `AllowBooleanLiterals` | No | Yes |
| `AllowUnderscoreSeparators` | No | Yes |
| `AllowSignednessSuffixes` | No | Yes |
| `AllowSnbtOperations` (`bool(...)`, `uuid(...)`) | No | Yes |

The parameterless parser and writer overloads use `SnbtOptions.v1_21_5`; there is deliberately no
`default` preset. `NaN`/`Infinity` literals, the rejection of `i`/`I` integer suffixes, string-key
compound rules, and typed-array suffix handling are dialect-independent.

#### Dialect effect on output

Only one flag changes the written text. With `AllowScientificNotation` disabled, floating-point values
are expanded into equivalent plain decimal literals — `1E+20d` is written as `100000000000000000000.0d` —
so the classic dialect can read them back. The expansion moves digits rather than re-formatting the
value, so the shortest round-trippable representation is preserved exactly. The other eight flags
describe input only: the writer never emits trailing commas, hexadecimal or binary literals,
underscores, signedness suffixes, or `bool()`/`uuid()` operations.

String quoting is dialect-independent on purpose. A bare string is only written when no dialect would
read it as a number, because the classic dialect treats a number-like token such as `.5` as a malformed
number and raises `SnbtParseException` instead of falling back to a bare string; quoted strings are valid
in every dialect. One case the writer cannot rescue is a heterogeneous `NbtList`: the element type
suffixes cannot express it, so writing such a list and parsing it back with `v1_13` fails.

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
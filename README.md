# Poly.NBT

PolyType-based serialization for the Java and Bedrock NBT wire formats, with SNBT text support.

NBT is Minecraft's binary data format. This library reads and writes it in all four dialects — Java Edition
files, the Java network protocol, Bedrock files, and the Bedrock network protocol — and gives you three ways
to work with a document: as your own .NET types, as a read-only tree of generic elements, or as SNBT text.

## Supported

| Area | Available |
|:---|:---|
| Dialects | `JavaEdition`, `JavaNetworkEdition`, `BedrockEdition`¹, `BedrockNetworkEdition`¹ |
| Types | Primitives, arrays, collections, dictionaries, objects, `Nullable<T>`, enums (mapped to the smallest integer tag for the underlying type, with no names written) |
| DOM | `NbtElement` and its twelve tag types, `NbtDocument`, `NbtList.TryToArray` |
| SNBT | `SnbtParser`, `SnbtWriter`, `SnbtOptions` (`v1_13` and `v1_21_5`) |
| AOT | Source-generated shapes pass the trim and AOT analyzers and need no reflection at run time; the reflection entry points are annotated unsafe and will not survive trimming² |

Not implemented: async APIs, polymorphic (union and derived-type) serialization, and built-in marshalers for
`decimal`, `Guid`, `DateTime`, and `DateTimeOffset`.

¹ Bedrock reuses the Java codecs with different endianness and number encodings, but has not been validated
against a real Bedrock corpus fixture.
² `IsAotCompatible` enables the trim and AOT analyzers for this project's own build. An end-to-end
`dotnet publish` is not run in this repository.

## Getting started

The library is not yet published to NuGet. Reference the project directly:

```xml
<ProjectReference Include="..\Poly.NBT\Poly.NBT.csproj" />
```

`dotnet pack Poly.NBT/Poly.NBT.csproj` produces a `Poly.NBT` package if a package reference suits you better.

The types live in `Poly.NBT` (serializer and options), `Poly.NBT.Dom` (the element tree), and `Poly.NBT.Snbt`
(text format).

### Round-trip an object

Annotate your model with `[GenerateShape]`. This is the source-generated path, and the one to prefer: it
works under trimming and Native AOT, and it needs no reflection at run time.

```csharp
using Poly.NBT;

[GenerateShape]
public partial record Player(int Health, string Name);
```

```csharp
NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaEdition);

// To and from a byte array. Both directions need the type's shape.
byte[] bytes = serializer.Serialize(player, "Player", Player.GetTypeShape());
Player? restored = serializer.Deserialize<Player>(bytes, Player.GetTypeShape());

// To and from a stream you already own. The shape is inferred, so it is not passed.
using (FileStream file = File.Create("player.dat"))
{
    serializer.Serialize(file, player, "Player");
}
```

The readers start at the stream's current position, so reset `Position` before reading one back.

```csharp
using (FileStream file = File.OpenRead("player.dat"))
{
    Player? fromFile = serializer.Deserialize<Player>(file);
}
```

If a model cannot be annotated, the reflection entry points take its place. They are annotated
`[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`, so they will not work under trimming or AOT.

```csharp
byte[] bytes = serializer.SerializeUsingReflection(player, "Player");
Player? restored = serializer.DeserializeUsingReflection<Player>(bytes);
```

### Read and write a document

When you do not have a model for the data — a file you are only inspecting, or one whose schema varies —
work with the DOM instead.

```csharp
NbtDocument document = serializer.DeserializeDocument(bytes);

NbtElement root = document.RootElement;
string name = document.RootTagName;

// Build one by hand and write it.
NbtCompound level = new(
    new KeyValuePair<string, NbtElement>("Health", new NbtInt(20)),
    new KeyValuePair<string, NbtElement>("Name", new NbtString("Player")));

serializer.Serialize(stream, new NbtDocument("Level", level));
```

## Choosing a dialect

Each preset is a whole wire format, not a set of tweaks. Pick the one that matches where the bytes come from
or are going.

| Preset | Use it for |
|:---|:---|
| `JavaEdition` | Files a Java Edition reader will open, and anything you want to diff against the game's own output. |
| `JavaNetworkEdition` | The Java network protocol — the same layout without the root name field. |
| `BedrockEdition` | Bedrock's on-disk format: little-endian, and no `TAG_Long_Array`. |
| `BedrockNetworkEdition` | Bedrock's network packets — also ZigZag VarInt numbers instead of fixed-width ones. |

Every preset is a `record struct`, so a variation is a `with` expression rather than a new configuration
object:

```csharp
NbtSerializer standard = NbtSerializer.Create(NbtOptions.JavaEdition);
NbtSerializer noArrays = NbtSerializer.Create(NbtOptions.JavaEdition with { SupportsLongArray = false });
```

A named dialect always writes the root name field and a network dialect omits it; the `rootTagName` argument
only supplies its contents. See [Root tag name](docs/internals.md#root-tag-name).

## Working with the DOM

Every NBT tag has a matching element type.

| NBT tag | Element | Payload |
|:---|:---|:---|
| `TAG_Byte` `TAG_Short` `TAG_Int` `TAG_Long` | `NbtByte` `NbtShort` `NbtInt` `NbtLong` | `sbyte` `short` `int` `long` |
| `TAG_Float` `TAG_Double` | `NbtFloat` `NbtDouble` | `float` `double` |
| `TAG_String` | `NbtString` | `string` |
| `TAG_Byte_Array` `TAG_Int_Array` `TAG_Long_Array` | `NbtByteArray` `NbtIntArray` `NbtLongArray` | `byte[]` `int[]` `long[]` |
| `TAG_List` | `NbtList` | ordered elements |
| `TAG_Compound` | `NbtCompound` | named entries |

`NbtList` is an `IReadOnlyList<NbtElement>` and `NbtCompound` is an `IReadOnlyDictionary<string, NbtElement>`,
so both work with ordinary LINQ. `NbtCompound` preserves insertion order, and that order is what the written
bytes and the SNBT text follow. The tree is read-only — neither indexer has a setter and there is no `Add` or
`Remove` — so to change a document, build the tree you want and write that, or go through a model type.

`ToElement` and `FromElement` convert between a typed value and the tree. Each call round-trips through the
wire format, so prefer a stream in a hot loop; see [Performance](docs/internals.md#performance).

`NbtList.TryToArray` converts a homogeneous list into a typed array in one step, with an overload per element
type:

```csharp
NbtList list = new(new NbtInt(1), new NbtInt(2), new NbtInt(3));

if (list.TryToArray(out int[]? values))
{
    Console.WriteLine(values.Length);
}
```

Overloads: `byte[]`, `sbyte[]`, `short[]`, `int[]`, `long[]`, `float[]`, `double[]`, `string[]`.

## SNBT text

SNBT is NBT written as text — the format of the `/data` command. It converts to and from the DOM only; it
never touches the binary serializer or PolyType.

```csharp
using Poly.NBT.Snbt;

NbtElement element = SnbtParser.Parse("{name:Bananrama,Health:20b,Pos:[1.0d,2.0d,3.0d]}");
string text = SnbtWriter.Write(element);

NbtDocument document = SnbtParser.ParseDocument(new StringReader(text));
SnbtWriter.Write(Console.Out, document);
```

On malformed input `SnbtParser` throws `SnbtParseException`, which carries the character `Offset` of the
failure.

Two dialects are provided. `v1_21_5` accepts every syntax extension Minecraft added up to 1.21.5 and is what
the parameterless overloads use; `v1_13` disables all of them, so it reads the classic grammar:

```csharp
NbtElement modern = SnbtParser.Parse("{a:1,b:2,}");                       // trailing comma: v1_21_5 only
NbtElement classic = SnbtParser.Parse("{a:1,b:2}", SnbtOptions.v1_13);

string forClassicReaders = SnbtWriter.Write(element, SnbtOptions.v1_13);
```

Write with the same options you read with when the text has to round-trip. See
[SNBT dialects](docs/internals.md#snbt-dialects) for which flags affect the written text, and for the full
escape set.

## Errors

| Exception | Raised when |
|:---|:---|
| `FormatException` | The bytes are structurally invalid — an unknown tag type, a negative length, a malformed string, an over-wide VarInt. |
| `InvalidDataException` | The bytes are valid but break a rule — a configured limit, a heterogeneous `TAG_List`, a missing root value. |
| `EndOfStreamException` | The document is truncated. |
| `SnbtParseException` | SNBT text is malformed. Carries `Offset`. |

See [Malformed input](docs/internals.md#malformed-input) for how the first three stay distinguishable.

## Requirements

- The `net10.0` target framework and a C# 14 compiler.
- Root values, list elements, and DOM payloads cannot be `null`; NBT has no null representation.
- Dictionary keys must be `string`.
- `NbtSerializer.Create` requires an explicit options value.
- `MaxDepth` and `MaxCollectionLength` are on by default, bounding one nesting level and one collection.
  Neither bounds a document's total size, so check the length of untrusted input before reading it.

## License

MIT. See [LICENSE](LICENSE). The fNbt test fixtures are BSD-3-Clause; see
[Third-party notices](docs/internals.md#third-party-notices).

## Going deeper

- [docs/internals.md](docs/internals.md) — the configuration reference, the reasoning behind each limit, the
  SNBT dialect and escape tables, performance, and the known limitations.
- API reference — every public member is documented in `Poly.NBT.xml`, which ships next to the assembly and
  is read by your editor.

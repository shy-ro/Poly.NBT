# Poly.NBT

PolyType-based, Native AOT-friendly serialization for the Java and Bedrock NBT wire formats.

NBT is Minecraft's binary data format. This library reads and writes it in all four dialects — Java Edition
files, the Java network protocol, Bedrock files, and the Bedrock network protocol — and gives you three ways
to work with a document: as your own .NET types, as a read-only tree of generic elements, or as SNBT text.

## Supported

| Area | Available |
|:---|:---|
| Dialects | `JavaEdition`, `JavaNetworkEdition`, `BedrockEdition`, `BedrockNetworkEdition` |
| Types | Primitives, arrays, collections, dictionaries, objects, `Nullable<T>`, enums |
| DOM | `NbtElement` and its twelve tag types, `NbtDocument`, `NbtList.TryToArray` |
| SNBT | `SnbtParser`, `SnbtWriter`, `SnbtOptions` (`v1_13` and `v1_21_5`) |
| AOT | Source-generated shapes are trim- and AOT-safe; the reflection entry points are not |

Not implemented: async APIs, polymorphic (union and derived-type) serialization, and built-in marshalers for
`decimal`, `Guid`, `DateTime`, and `DateTimeOffset`.

## Getting started

The library is not published to NuGet; add it as a project or package reference.

```xml
<ProjectReference Include="..\Poly.NBT\Poly.NBT.csproj" />
```

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

To read a stream back, restore its position first — the readers start wherever the stream is.

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

`Create` requires an explicit options value — there is no parameterless overload, so a dialect is never chosen
by accident.

## Root tag name

A named dialect always writes the name field, so an empty name is written as an empty name rather than left
out. Whether the field exists at all is a property of the dialect, not of the call: the `rootTagName`
argument only supplies its contents.

```csharp
NbtSerializer named = NbtSerializer.Create(NbtOptions.JavaEdition);          // tag + name + payload
NbtSerializer network = NbtSerializer.Create(NbtOptions.JavaNetworkEdition); // RootTagNaming = Omitted

named.Serialize(stream, value, "level", shape);   // name field, contents "level"
named.Serialize(stream, value, "", shape);        // name field, contents empty
network.Serialize(stream, value, "level", shape); // no name field at all
```

Reading a name-less stream with a named dialect needs an explicit opt-in, which exists only for non-standard
input:

```csharp
MyModel? restored = named.Deserialize(stream, shape, rootNameOmitted: true);
```

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
so both work with ordinary LINQ. A `NbtCompound` preserves insertion order, and that order is what the SNBT
text and the wire bytes follow.

The tree is read-only: neither indexer has a setter and there is no `Add` or `Remove`, so `compound["x"] = y`
will not compile. To change a document, build the tree you want and write that, or go through a model type,
where properties are ordinary and mutable.

`NbtList.TryToArray` converts a homogeneous list into a typed array in one step, with an overload per
element type:

```csharp
NbtList list = new(new NbtInt(1), new NbtInt(2), new NbtInt(3));

if (list.TryToArray(out int[]? values))
{
    // values is [1, 2, 3]
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
SnbtWriter.Write(Console.Out, document);   // streams, never materializing the whole document
```

On malformed input `SnbtParser` throws `SnbtParseException`, which carries the character `Offset` of the
failure — the single most useful number when a hand-written file will not load.

Two dialects are provided. `v1_21_5` accepts every syntax extension Minecraft added up to 1.21.5 and is what
the parameterless overloads use; `v1_13` disables all of them, so it reads the classic grammar:

```csharp
NbtElement modern = SnbtParser.Parse("{a:1,b:2,}");                       // trailing comma: v1_21_5 only
NbtElement classic = SnbtParser.Parse("{a:1,b:2}", SnbtOptions.v1_13);

string forClassicReaders = SnbtWriter.Write(element, SnbtOptions.v1_13);
```

Write with the same options you read with when the text has to round-trip — one flag changes the written text
(`AllowScientificNotation` off expands `1.0E20d` into a plain decimal literal), and the element type suffixes
cannot express a heterogeneous list. See [SNBT dialects](docs/internals.md#snbt-dialects) for the full flag
table and the escape set.

## Common tasks

### Read a document you did not write

A length prefix in the format drives an allocation before any payload is read, so a hostile or corrupt file
can ask for a lot of memory. `NbtOptions.MaxCollectionLength` and `MaxDepth` bound that, and both are on by
default.

```csharp
NbtOptions defensive = NbtOptions.JavaEdition with
{
    MaxDepth = 64,
    MaxCollectionLength = 1 << 20,
};
NbtSerializer serializer = NbtSerializer.Create(defensive);
```

They are per-collection limits, so they do not bound a document's total size. When the input is untrusted,
check its length before handing the stream over. See
[Collection and string lengths](docs/internals.md#collection-and-string-lengths).

### Convert between an object and the DOM

`ToElement` and `FromElement` bridge the model and the tree, so you can serialize a typed graph and then walk
it as generic elements — or build a tree and hand it to a typed deserializer.

```csharp
NbtElement tree = serializer.ToElement(player, Player.GetTypeShape());
Player? back = serializer.FromElement(tree, Player.GetTypeShape());
```

Each call is a full serialize plus a full deserialize through an intermediate buffer. That is fine for
occasional bridging; in a hot loop, write to a stream instead. See
[Performance](docs/internals.md#performance).

### Choose a shape you do not own

PolyType attributes work through the serializer unchanged, so a property can be renamed or skipped without
touching the model's shape:

```csharp
[GenerateShape]
public partial class Entity
{
    [PropertyShape(Name = "id")]
    public int Identifier { get; set; }

    [PropertyShape(Ignore = true)]
    public string CacheKey { get; set; } = "";
}
```

See [PolyType attributes](docs/internals.md#polytype-attributes).

## Errors

| Exception | Raised when |
|:---|:---|
| `FormatException` | The bytes are structurally invalid — an unknown tag type, a negative length, a malformed string, an over-wide VarInt. |
| `InvalidDataException` | The bytes are valid but break a rule — a configured limit, a heterogeneous `TAG_List`, a missing root value. |
| `EndOfStreamException` | The document is truncated. |
| `SnbtParseException` | SNBT text is malformed. Carries `Offset`. |

The split is deliberate: it separates bad bytes from bytes this configuration will not accept, so a caller
can tell corruption from a limit it needs to raise. See [Malformed input](docs/internals.md#malformed-input).

## Requirements

- Root values and list elements cannot be `null`; NBT has no null representation.
- A DOM payload cannot be `null` — `NbtString(null)` throws `ArgumentNullException`.
- Dictionary keys must be `string`.
- Enums map to the smallest integer tag for their underlying type; no names are written.
- `NbtSerializer.Create` requires explicit options.

## Going deeper

[docs/internals.md](docs/internals.md) covers the configuration reference, the reasoning behind each limit,
the SNBT dialect and escape tables, the known limitations, and the performance characteristics.

To read the API reference, build the library and open `Poly.NBT.xml` next to the assembly, or point your
editor at the generated XML — every public member is documented in place.

## Acknowledgements

The fixtures `test.nbt` and `bigtest.nbt` in `Poly.NBT.Tests/TestFiles` originate from
[fNbt](https://github.com/mstefarov/fNbt) and are retained under BSD-3-Clause in
`Poly.NBT.Tests/TestFiles/fNbt-LICENSE.txt`.

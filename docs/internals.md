# Poly.NBT — internals and dialect notes

The reference for what the library does in the awkward cases: the full configuration surface, where the
behavior deliberately departs from the reference implementations, and the reasoning behind each limit. The
[README](../README.md) covers getting started; this document is where a fact lives once it needs more than a
sentence.

## Contents

- [Configuration notes](#configuration-notes)
- [Root tag name](#root-tag-name)
- [Nesting depth](#nesting-depth)
- [Collection and string lengths](#collection-and-string-lengths)
- [Lists](#lists)
- [Object model and converters](#object-model-and-converters)
- [Malformed input](#malformed-input)
- [PolyType attributes](#polytype-attributes)
- [SNBT dialects](#snbt-dialects)
- [Performance](#performance)
- [Packaging](#packaging)
- [Limitations](#limitations)
- [Third-party notices](#third-party-notices)

## Configuration notes

Member-by-member reference for `NbtOptions` and its four enums is in the generated `Poly.NBT.xml`, which
ships next to the assembly: every public member is documented in place, and an editor reads it. What follows
is only what a signature cannot show — which preset to reach for, how the values behave as values, and why
you would touch each of the four levers that are not a preset.

`NbtOptions` is a `readonly record struct` with three enums and two capability flags. Because the flags are
plain `bool`s rather than nullable, two options values compare equal exactly when their properties read the
same, so an options value is usable as a dictionary key and a `with` expression produces a predictable
result.

### Which preset

Four presets cover every dialect this library is meant to be used with. All four enable
`OptimizePrimitiveListsToArrays`, because every real Minecraft dialect writes primitive collections as the
matching array tag.

| Preset | Endianness | Strings | Numbers | Root | `SupportsLongArray` |
|:---|:---|:---|:---|:---|:---:|
| `JavaEdition` | big | Modified UTF-8 | fixed | named | yes |
| `JavaNetworkEdition` | big | Modified UTF-8 | fixed | omitted | yes |
| `BedrockEdition` | little | UTF-8 with escapes | fixed | named | no |
| `BedrockNetworkEdition` | little | UTF-8 with escapes | ZigZag VarInt | omitted | no |

Every preset sets `MaxDepth` and `MaxCollectionLength` to their defaults explicitly, so reading them back
gives the number that is actually in force rather than the zero sentinel.

`NbtSerializer.Create` also accepts `default(NbtOptions)`. That is not a preset and is not intended to be
one, but it is coherent — big-endian, Modified UTF-8, fixed-width numbers, a named root — and simply leaves
both capability flags off, so `OptimizePrimitiveListsToArrays` is `false` there and primitive collections are
written as homogeneous `TAG_List`s.

### When to change something

- **`SupportsLongArray`** — turn it off only when targeting a Bedrock reader, which has never understood
  `TAG_Long_Array`. Java has supported it since 1.12.
- **`OptimizePrimitiveListsToArrays`** — turn it off to force a primitive collection to be written as a
  homogeneous `TAG_List` instead of the matching array tag. Both layouts are legal and either is read back,
  but the array form is what Minecraft itself produces, so leaving it on keeps output byte-comparable with
  the game.
- **`MaxDepth`** — lower it when reading input whose nesting you do not trust. See
  [Nesting depth](#nesting-depth).
- **`MaxCollectionLength`** — lower it for the same reason, and raise it for a document that legitimately
  holds more than the default in one collection. Set it on both ends when exchanging such a document. See
  [Collection and string lengths](#collection-and-string-lengths).
- **`Endianness`, `StringEncoding`, `NumericEncoding`, `RootTagNaming`** — these four *are* the dialect, and
  a preset already sets all of them coherently. Change one only for a format no preset describes, such as a
  writer that emits strict UTF-8.

## Root tag name

A named dialect always writes the name field, so an empty name is written as an empty name (`00 00`) rather
than omitted. Java's `NbtIo` consumes that field unconditionally, so leaving it out produced a document no
Java reader could parse.

Whether the field exists at all is a property of the dialect, not of the call. `NbtRootTagNaming` decides it;
the `rootTagName` argument only supplies its contents when the field is present.

```csharp
NbtSerializer named = NbtSerializer.Create(NbtOptions.JavaEdition);          // tag + name + payload
NbtSerializer network = NbtSerializer.Create(NbtOptions.JavaNetworkEdition); // RootTagNaming = Omitted

// Both of these write a two-byte empty name.
named.Serialize(stream, value, "", shape);
named.Serialize(stream, value, "level", shape);

// JavaNetworkEdition writes no name field at all, whatever the name argument says.
network.Serialize(stream, value, "level", shape);

// Reading a name-less stream with a named dialect is an explicit opt-in for non-standard input.
MyModel? restored = named.Deserialize(stream, shape, rootNameOmitted: true);
```

`rootNameOmitted: true` is the only way to read a name-less stream with a named dialect. It exists for
non-standard input; a document produced by a named dialect never needs it.

## Nesting depth

Both readers and writers recurse once per nesting level, and the level count comes from the input, so the
depth is bounded. `NbtOptions.MaxDepth` and `SnbtOptions.MaxDepth` default to 512, counting the outermost
value as level one. Past the limit the binary reader and both writers throw `InvalidDataException`, and the
SNBT parser throws `SnbtParseException` carrying the `Offset` of the offending bracket; the message names the
option to raise. Zero selects the library default.

The bound exists because `StackOverflowException` cannot be caught in .NET: without it, 15 KB of nested
`TAG_List` headers or 6 KB of nested brackets terminate the process, which no host can defend against.
Writing is bounded the same way, so a caller-built tree cannot overflow the stack either.

## Collection and string lengths

A length prefix is four bytes (or a VarInt) that immediately drives an allocation, so a hostile document can
ask for two gigabytes before one payload byte has been read. `NbtOptions.MaxCollectionLength` caps both the
element count of a collection and the encoded byte length of a string. It applies to reading and to writing,
defaults to `1 << 24` elements, and takes zero to mean the library default. Past the limit reads and writes
throw `InvalidDataException`, and a negative limit is rejected when the serializer is created.

The default admits every realistic document — a whole 16³ chunk section is a few thousand elements — while
keeping the worst case for a hostile `TAG_Long_Array` in the low hundreds of megabytes. A truncated
collection is still reported as `EndOfStreamException`, not as an oversized one, so the two conditions stay
distinguishable.

The limit is per collection. It does not bound a document's *total* size: a broad tree of individually legal
collections can still add up. When accepting NBT from an untrusted source, bound the input as well — reject
streams larger than a fixed number of bytes before handing them to `NbtSerializer`. Minecraft's
`NbtAccounter` solves the same problem with running total accounting; `MaxCollectionLength` is the per-value
equivalent.

Both limits are per-value, so input of unknown provenance lowers them together:

```csharp
NbtOptions defensive = NbtOptions.JavaEdition with
{
    MaxDepth = 64,
    MaxCollectionLength = 1 << 20,
};
NbtSerializer serializer = NbtSerializer.Create(defensive);
```

`MaxDepth` counts nesting levels rather than collection entries; see [Nesting depth](#nesting-depth).

## Lists

A `TAG_List` is homogeneous: the header declares one element type and every element has to match it. The
empty list is the single exception. Its element type carries no information — there are no elements for it to
disagree with — and writers differ on what to put there: this library and Minecraft both write `TAG_End`, but
the format does not require it. Reading therefore tolerates any element type when the length is zero, on the
DOM and the typed paths alike, so `09 08 00 00 00 00` (an empty list declared `TAG_String`) reads as an empty
list whatever it is materialized into, and the two paths cannot disagree about it. Writing still emits
`TAG_End`. A non-empty list that declares `TAG_End`, or whose elements disagree with the declared type, is
still rejected.

## Object model and converters

The rules below are properties of the format rather than of this implementation, so they are invariants a
caller has to work within rather than configuration.

- **Dictionary keys must be `string`.** Any other key type throws `NotSupportedException` during converter
  construction.
- **Enums map to the smallest integer tag for their underlying type.** `byte`/`sbyte` → `TAG_Byte`,
  `short`/`ushort` → `TAG_Short`, `int`/`uint` → `TAG_Int`, `long`/`ulong` → `TAG_Long`. No string-based names
  are emitted.
- **`NbtCompound` preserves insertion order.** `Keys`, `Values`, and enumeration follow insertion order, and
  that order decides both the SNBT text and the wire bytes, so it is documented rather than left to the
  backing collection. Equality and hashing are order-independent, matching NBT's own unordered-map semantics:
  two compounds with the same entries in different orders are equal.
- **The DOM is immutable.** Every element record exposes a get-only payload, and neither `NbtList`'s nor
  `NbtCompound`'s indexer has a setter. There is no `Add` or `Remove`, so `compound["x"] = y` does not
  compile: a document is changed by building the tree you want, or by going through a model type, whose
  properties are ordinary and mutable.
- **A payload is never absent.** `NbtString`, `NbtByteArray`, `NbtIntArray`, and `NbtLongArray` reject a null
  payload with `ArgumentNullException`. A `with` expression replaces the property without running the
  constructor, so it can still put one in; the writers then report `InvalidDataException` rather than
  dereferencing it. `NbtList` and `NbtCompound` deliberately do not scan for null elements — their
  constructors already copy their input, and a second pass would add a full traversal to the read path for a
  mistake the writers already catch.
- **`NbtSerializer.Create` requires explicit options.** There is no parameterless overload, so a dialect is
  never chosen by accident.

`decimal`, `Guid`, `DateTime`, and `DateTimeOffset` have no built-in marshaler; supply a custom one through
PolyType's `[TypeShape(Marshaler = ...)]`. Polymorphic (union and derived-type) serialization is not
implemented. There is no async API: the readers and writers are synchronous.

## Malformed input

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

## PolyType attributes

A model's shape reaches the serializer unchanged, so PolyType's own attributes apply without an NBT-specific
wrapper: a property can be renamed or skipped, and a type can be given a custom marshaler.

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

## SNBT dialects

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

The parameterless parser and writer overloads use `SnbtOptions.v1_21_5`; there is deliberately no `default`
preset, so a dialect is never chosen by accident. `NaN`/`Infinity` literals, the rejection of `i`/`I` integer
suffixes, string-key compound rules, typed-array suffix handling, and the string escape set are
dialect-independent.

A number literal may begin with a sign, and — where `AllowOmittedFloatParts` permits it — the integer or
fractional part may be left out entirely, so `+.5`, `-.5`, `-5.`, and `5.` are all numbers rather than
strings. A token that begins with a sign or a point but is not a literal, such as `-foo` or `.foo`, is read
as a bare string by the parser; the writer nevertheless quotes it, because Minecraft's tokenizer attempts a
numeric parse first and reports an error instead of falling back.

### String escapes

Both quote styles accept the same twelve escape sequences. The grammar does not give single-quoted strings a
smaller set, and the writer emits escapes in every dialect — a string containing a newline is written
`"a\nb"` even for `v1_13` — so gating them by dialect would make the writer's own output unreadable.

| Escape | Meaning |
|:---|:---|
| `\b` `\f` `\n` `\r` `\s` `\t` | Backspace, form feed, line feed, carriage return, space, tab |
| `\\` `\'` `\"` | Backslash, single quote, double quote |
| `\xhh` | Two hexadecimal digits, producing a single code unit in `U+0000`–`U+00FF` |
| `\uhhhh` | Four hexadecimal digits, producing one UTF-16 code unit |
| `\UHHHHHHHH` | Eight hexadecimal digits, producing a Unicode code point — a surrogate pair when it is above `U+FFFF` |

`\N{name}` is the thirteenth escape and is not supported; see [Limitations](#limitations).

### Dialect effect on output

Only one flag changes the written text. With `AllowScientificNotation` disabled, floating-point values are
expanded into equivalent plain decimal literals — `1.0E20d` is written as `100000000000000000000.0d` — so the
classic dialect can read them back. The expansion moves digits rather than re-formatting the value, so the
shortest round-trippable representation is preserved exactly. The other eight flags describe input only: the
writer never emits trailing commas, hexadecimal or binary literals, underscores, signedness suffixes, or
`bool()`/`uuid()` operations.

Floating-point literals follow the shape Java's `Double.toString`/`Float.toString` produce, because that is
what Minecraft prints:

| Rule | Example |
|:---|:---|
| The mantissa always keeps a decimal point and one digit after it. | `1.0E20d`, `100.0d`, `0.0d` |
| The exponent carries no `+` and no leading zeros. | `1.2345678901234568E17d` |
| `E`-notation is used outside `[10^-3, 10^7)`. | `1.0E7d` and `1.0E-4d`, but `9999999.0d` and `0.001d` |

The digits themselves come from the runtime's shortest round-trippable form, which is not always the same
digits Java picks: for the smallest subnormals Java prints `4.9E-324`, the runtime prints `5E-324`. Both parse
back to the same value, so this is the one remaining textual difference. The point of the alignment is that a
document written here and one printed by the game agree byte for byte in every ordinary case, which makes
diffs, checksums, and cache keys over SNBT output meaningful.

### Quote selection

String quoting is dialect-independent on purpose. A bare string is only written when no dialect would read it
as a number, because the classic dialect treats a number-like token such as `.5` as a malformed number and
raises `SnbtParseException` instead of falling back to a bare string; quoted strings are valid in every
dialect. A leading `-`, `+`, or `.` is reserved by the grammar the same way a leading digit is, so `-foo` is
written as `"-foo"`: Minecraft's tokenizer tries a numeric parse first and errors out rather than falling back
to a bare string, even though this library's parser does recover. One case the writer cannot rescue is a
heterogeneous `NbtList`: the element type suffixes cannot express it, so writing such a list and parsing it
back with `v1_13` fails.

When a string does need quoting, the writer picks the quote character that needs no escaping: `a"b` is
written `'a"b'` and `a'b` is written `"a'b"`. With both kinds present there is no escape-free choice, so
double quotes win and the double quotes inside are escaped — `a"b'c` becomes `"a\"b'c"`. The Wiki's
"Conversion to SNBT" section records a different rule, picking the opposite of whichever quote appears first,
for the `/data get` path. That path always quotes, whereas this writer produces bare strings wherever the
grammar allows them, so the two are not the same rule applied to the same input; both forms parse to the same
value, which the tests assert rather than assume.

## Performance

The readers and writers work directly on a `Stream` and never buffer a whole document, so an object graph is
materialized once. Several properties are worth knowing before putting this in a hot loop.

- **The `ReadOnlySpan<byte>` overloads copy.** They exist so that a caller holding a buffer does not have to
  construct a `Stream`, not to avoid a copy — the readers are stream-based and the BCL has no read-only span
  adapter for `Stream`. In a loop, keep one `MemoryStream` and reset it (`Position = 0`) instead of calling
  the span overloads repeatedly.
- **`ToElement` and `FromElement` round-trip through the wire format.** Each call is a full serialize plus a
  full deserialize with an intermediate byte buffer. That keeps one traversal implementation instead of two
  that have to be kept in step, but it makes the conversion cost the same as writing and reading the document
  would. With a stream already in hand, `Serialize` writes the same bytes without the second pass. The
  conversion is also subject to `MaxDepth` and `MaxCollectionLength` like any other read.
- **A VarInt dialect reads one byte at a time.** On `BedrockNetworkEdition` every `TAG_Int` and `TAG_Long`
  decodes through a per-byte `ReadByte`, which is free on a `MemoryStream` and expensive on an unbuffered
  network stream. Wrap such a stream in a `BufferedStream` before handing it over.
- **SNBT parsing avoids copying where it can.** A numeric literal's sign, digit separators, and any omitted
  integer or fractional part are rewritten into the shape the runtime's own parser expects, in a stack buffer
  — a pooled one for a pathologically long literal — and parsed from the span, so a number token produces no
  intermediate `string`. A quoted string with no escape is returned as a slice of the input directly, so only
  a string that actually holds an escape is built up character by character.

The DOM bridge is the one conversion whose per-call cost is worth spelling out:

```csharp
NbtElement tree = serializer.ToElement(player, Player.GetTypeShape());
Player? back = serializer.FromElement(tree, Player.GetTypeShape());
```

Primitive arrays transfer in one call each for a fixed-width dialect: 100,000 elements cost a single
`ReadExactly` or `Write` over `MemoryMarshal.AsBytes`, with an `ArrayPool` byte-swap only when the dialect's
byte order disagrees with the machine's. Scalars are read through `stackalloc` buffers, so reading an `int` or
a string allocates nothing beyond the string itself.

The allocation tests in `Poly.NBT.Tests` are the standing guard on these paths. Each asserts a per-item or
per-character byte budget, so a change that reintroduces a temporary fails the suite rather than only showing
up as a slower build. There is deliberately no `BenchmarkDotNet` project: a threshold that runs in CI is
deterministic where a benchmark on shared hardware is not, and the comparisons quoted above came from
throwaway probes run against the two revisions rather than from a harness kept in the repository.

## Packaging

`dotnet pack Poly.NBT/Poly.NBT.csproj` writes two packages. `Poly.NBT.<version>.nupkg` carries the assembly,
the generated `Poly.NBT.xml`, the README, and the LICENSE. `Poly.NBT.<version>.snupkg` is the symbol package
and carries the PDB, and nothing else: the two are separate so that symbols are downloaded only by a debugger
that asks for them. Both go to nuget.org with `dotnet nuget push`, the symbol package under the same id.

The PDB is portable and carries a Source Link document, so a debugger holding it fetches the exact source for
the commit the package was built from instead of showing a decompiled approximation. No package reference is
needed for that: the .NET SDK bundles the GitHub provider and enables it by default, so `Microsoft.SourceLink.*`
appears nowhere in the project file, and adding one would suppress the bundled provider rather than improve it.
The provider comes from `RepositoryUrl` and the commit comes from the repository at build time, which is why
source stepping only works for a commit that has been pushed.

`ContinuousIntegrationBuild` is set only when `CI` or `TF_BUILD` is true. It rewrites every source path to
`/_/` before the compiler sees it, so the published PDB does not depend on where the build ran; leaving it off
for a local build is what keeps the debugger reading the working tree instead of the last commit. A release
built anywhere should therefore be packed as `CI=true dotnet pack`.

One gap is worth knowing about: source-generated code cannot be source-linked. PolyType's DOM converter and
shape providers are added to the compilation in memory, and Roslyn records them under their bare hint name
(`Poly.NBT.Dom.NbtElement.g.cs`) rather than a path under the repository root, so neither the Source Link
mapping nor a debugger can resolve them — a step into generated code, the shape provider for `NbtElement` for
instance, has no source to show. `EmitCompilerGeneratedFiles` writes those files to `obj/` but does not change
the name they are recorded under, so it does not close the gap and is left off.

## Limitations

- **Bedrock is not corpus-validated.** `BedrockEdition` and `BedrockNetworkEdition` reuse the Java codecs with
  a different endianness, string encoding, and number encoding, and the unit tests cover each of those
  behaviors against hand-built bytes. What has never been done is reading a fixture captured from a real
  Bedrock world or packet, so Bedrock support is asserted by construction rather than proven against the game.
- **`\N{name}` is not supported.** SNBT defines thirteen string escapes; twelve are implemented in both quote
  styles, and the thirteenth indexes Unicode's name database (`\N{Snowman}`). A partial table would accept
  some names and silently reject others with no way for a caller to tell an unsupported name from a misspelled
  one, so it is refused with an error that names it. Twelve of thirteen is enough for every escape the game
  emits, since Minecraft does not write `\N{name}` either.
- **`Utf8WithEscapes` literal escape ambiguity.** If a string contains the literal sequence `ESC x HH` (a
  `U+001B` character followed by `x` and two hex digits), the encoded form loses the `x` and hex digits on
  round-trip. This is a rare boundary; the fix would significantly increase complexity and is not planned.
- **Asymmetric `long[]` deserialization.** When `OptimizePrimitiveListsToArrays` is `false`, `long[]` and
  `List<long>` still read `TAG_Long_Array` input. Deserialization is driven by the actual tag on the wire and
  is not constrained by the serializer's output configuration.
- **Native AOT is not verified end to end.** The library is marked `IsAotCompatible`, which enables the trim,
  single-file, and Native AOT analyzers for its own build, so it cannot acquire an unannotated reflection
  dependency without a build warning. The reflection-based entry points carry `[RequiresUnreferencedCode]` /
  `[RequiresDynamicCode]`. What this guarantee does not include is an end-to-end proof: the repository
  contains no `PublishAot` project and runs no `dotnet publish -r <rid>`, so a consumer that needs Native AOT
  should run its own publish against its own target. On Windows that publish needs the MSVC linker from the
  "Desktop development with C++" workload, which is a .NET Native AOT requirement and not a dependency of
  this library.

## Third-party notices

The fixtures `test.nbt` and `bigtest.nbt` in `Poly.NBT.Tests/TestFiles` originate from
[fNbt](https://github.com/mstefarov/fNbt) and are retained under BSD-3-Clause in
`Poly.NBT.Tests/TestFiles/fNbt-LICENSE.txt`. They are test input and are not redistributed with the library
package, which is MIT — see [LICENSE](../LICENSE).

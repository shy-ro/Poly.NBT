# Task Log

## 2026-09-25 - License Poly.NBT under MIT

### Scope

The library carried no license. Two earlier entries recorded that as a deliberate gap: the packaging entry
left `PackageLicenseExpression` out on the grounds that naming a license in the package would assert a choice
the project had not made, and left a comment in the project file saying the file and the expression had to be
added together. The choice is now MIT, which turns that comment into the change itself.

### Actual Changes

- `LICENSE` added at the repository root: the standard MIT text, with the year taken from the system clock and
  the copyright holder from the project's own `Authors` value (`shy-ro`), so the two cannot drift.
- The packaging-metadata comment in `Poly.NBT.csproj` is gone, replaced by
  `<PackageLicenseExpression>MIT</PackageLicenseExpression>` in the same property group. The comment existed
  only to record the absence; the absence is the thing being fixed.
- The package now carries a copy of `LICENSE` at its root, next to the already-packed `README.md`. Under an
  SPDX expression NuGet does not require the text, but the package is published on its own and the README's
  relative `[LICENSE](LICENSE)` link would otherwise resolve to nothing inside it. `PackageLicenseFile` is not
  used and cannot be combined with `PackageLicenseExpression`.
- README gains a `## License` section between `Requirements` and `Going deeper`: one sentence and the link.

### Verification

- `dotnet pack Poly.NBT/Poly.NBT.csproj`: produced `Poly.NBT.0.1.0.nupkg` with no warnings. The package was
  opened and its `.nuspec` read back, which now carries `<license type="expression">MIT</license>` and the
  derived `https://licenses.nuget.org/MIT`; the archive listing contains `LICENSE` and `README.md` at the
  root alongside `lib/net10.0/Poly.NBT.dll` and `Poly.NBT.xml`. The temporary output was removed afterwards.
- `dotnet build Poly.NBT.slnx`: 0 warnings, 0 errors. `dotnet test Poly.NBT.slnx --no-build --no-restore`:
  209/209 passed. `dotnet format --verify-no-changes --severity warn` and `git diff --check`: clean.
- Only packaging and documentation changed; no source or test file is touched by this entry.

### Known Issues and Next

- The two fNbt fixtures in `Poly.NBT.Tests/TestFiles` stay under BSD-3-Clause and keep their own
  `fNbt-LICENSE.txt`. The README's `Acknowledgements` section still carries that notice; the next entry moves
  it out of the user-facing document.
- The README's `not published to NuGet` line predates the packaging metadata and contradicts it, and its
  `Native AOT-friendly` claim has no end-to-end evidence behind it. Both are fixed in the next entry.
- `VersionPrefix` stays at `0.1.0`. The license does not change that; nothing has shipped yet.

## 2026-09-25 - Split the README into a usage guide and an internals reference

### Scope

The README had grown into a reference document. It opened with a feature matrix and then explained the root
name field, the depth and length limits, the malformed-input taxonomy, the SNBT dialect flags, the escape set,
the float format, and the quoting rule before it showed a second code sample. That material is worth having,
but not in the first thing a reader sees when all they want is to round-trip a file.

### Actual Changes

- README rewritten as a usage guide: what the library does, how to reference it, a first round trip, choosing
  a preset, the root-name rule, the DOM, SNBT, four task-oriented recipes, the exception table, and the
  requirements. It links into the reference for depth rather than inlining it.
- `docs/internals.md` added for the rest: the preset matrix, the root-name reasoning, the depth and length
  limits, the list rule, the object-model invariants, the malformed-input taxonomy, the PolyType attributes,
  the SNBT dialect flag table and escape table, the float format and quoting rules, performance, and the known
  limitations.
- Its configuration section keeps only what a signature cannot show — the preset comparison, the value
  semantics, and when to change each non-preset lever. The per-enum and per-property tables were dropped:
  `Poly.NBT.xml` already documents every one of those members in more detail, and a hand-copied second copy is
  a drift source with no upside.
- Two defects in the old README fixed:
  - The `Basic` sample called `serializer.Deserialize<MyModel>(bytes)`. Only the `Stream` overloads infer the
    shape from `IShapeable<T>`; the `byte[]` overloads are declared over `ReadOnlySpan<byte>` and require an
    explicit `ITypeShape<T>`, so that line could not compile. The guide now passes the shape and explains
    which overloads need one.
  - The new text initially called the DOM mutable. `NbtList`'s and `NbtCompound`'s indexers are get-only and
    neither type has `Add` or `Remove`, so `compound["x"] = y` does not compile. The guide states the tree is
    read-only and points at a model type for mutation.

### Verification

- Every cross-document anchor was checked against the target headings: `#collection-and-string-lengths`,
  `#malformed-input`, `#performance`, `#polytype-attributes`, and `#snbt-dialects` all resolve in
  `docs/internals.md`, and each of the eleven entries in its contents list resolves to a heading.
- `dotnet package search Poly.NBT` returns no results on nuget.org, which is what the guide's "not published
  to NuGet" line asserts.
- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors. `dotnet test Poly.NBT.slnx --no-restore`:
  209/209 passed. Documentation only; no source or test file changed.

### Known Issues and Next

- **DocFX was evaluated and rejected.** `GenerateDocumentationFile` already produces a complete
  `Poly.NBT.xml`, so the question was only whether to render a browsable site from it. DocFX 2.80.1 installs,
  restores, and runs `metadata` against `net10.0` and C# 14 with `0 warning(s) 0 error(s)`, and emits 30 type
  files with `NbtSerializer` and `NbtOptions` complete. Its metadata extractor does not understand a C# 14
  `extension` block, however, and drops every member inside one *silently*: `NbtElementExtensions.yml` came
  out as `children: []`, and `ToElement`, `FromElement`, `ToElementUsingReflection`, and
  `FromElementUsingReflection` appear nowhere in the generated output. `Poly.NBT.xml` does contain all four,
  so the XML-to-IntelliSense path is complete where the XML-to-website path is not. 2.80.1 is the latest
  published version, so this is not a matter of an outdated tool. Because the audit's section 7.1 calls the
  `extension` block the correct C# 14 idiom, the code was not rewritten to accommodate the tool; the trial's
  `docfx.json` and tool manifest were removed. Revisit if DocFX learns the syntax.

## 2026-09-25 - Close the audit: fix the ignore rule and record the benchmark decision

### Scope

The last two open audit items are housekeeping rather than defects, and both are decisions rather than
fixes, so they close together: P3-8, where `.gitignore` declared `TASK_LOG.md` ignored while the file was
tracked, and the benchmark project that the coverage table listed as missing.

### Actual Changes

- `.gitignore` no longer lists `TASK_LOG.md`, and the file stays tracked. The log is kept deliberately: it
  records the scope, the reasoning, and the verification for every commit in this series, which is history
  the repository would otherwise lose, and it is actively maintained rather than vestigial.
- README `Performance` closes with the benchmark decision and names the allocation tests as the guard that
  stands in for one. The same paragraph fixes a stale count - the section said "Three properties" while
  carrying four bullets after the SNBT entry was added.

### Verification

- `git check-ignore -v TASK_LOG.md` now reports nothing; before the change it also reported nothing, which
  is the whole reason this went unnoticed. `git check-ignore --no-index -v TASK_LOG.md` reported
  `.gitignore:14`, because without `--no-index` `check-ignore` silently omits files that are tracked, so the
  rule looked as though it matched nothing. `git ls-files --error-unmatch TASK_LOG.md` confirms the file is
  tracked both before and after.
- `AGENTS.md` is listed in `.gitignore` and is *not* tracked, so it is correct as written and was left
  alone. The audit's earlier note calling it a second instance of the same problem was wrong; it is the only
  one.
- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors. `dotnet test Poly.NBT.slnx --no-restore`:
  209/209 passed. `dotnet format --verify-no-changes --severity warn`: clean. No source or test file was
  touched by this entry.

### Known Issues and Next

The audit is closed. Two items remain open by decision rather than by oversight, and both are stated in the
README rather than left to be rediscovered:

- Native AOT is not verified end to end - see the P3-7 entry above for what was tried and what the analyzer
  pass does cover.
- `\N{name}` in SNBT is refused by design rather than implemented - see the P1-3 entry. A partial Unicode
  name table would accept some names and reject others with no way for a caller to tell a misspelling from a
  gap, so it is recorded in `Limitations` instead.

## 2026-09-25 - Document the AOT guarantee instead of adding an AOT smoke project

### Scope

The audit's P3-7 proposed a `PublishAot` smoke project so that Native AOT compatibility would be shown by an
actual publish rather than only asserted by the analyzers that `IsAotCompatible` turns on. A project was
written and exercised; the decision taken afterwards was not to keep it. What is recorded here is the
expanded documentation and the evidence behind that call.

### Actual Changes

- README `Requirements`, AOT bullet - now separates the analyzer guarantee from an end-to-end proof. It says
  what `IsAotCompatible` enables for the library's own build, keeps the `[RequiresUnreferencedCode]` /
  `[RequiresDynamicCode]` note on the reflection entry points, and states plainly that the repository contains
  no `PublishAot` project and runs no `dotnet publish -r <rid>`, so a consumer that needs Native AOT should
  run its own publish against its own target. It also notes that a Windows AOT publish needs the MSVC linker
  from the "Desktop development with C++" workload, which belongs to .NET Native AOT and not to this library.
- No project added and no change to `Poly.NBT.slnx`. The trial project was removed, so the solution is
  byte-for-byte as it was.

### Verification

The trial was run before being dropped, so both the failure mode and the honest limit are known:

- `dotnet build` of the trial project: 0 warnings with the trim, single-file, and Native AOT analyzers
  enabled. This is the half worth having and it needs no toolchain, because it covers the *consumer* side that
  the library's own build cannot see - an unannotated public API only warns at a call site.
- `dotnet publish -c Release -r win-x64`: the managed and AOT compile phases finished with no `IL2xxx` or
  `IL3xxx` diagnostics; only the final native link failed, because this machine has no Visual Studio and no
  `vswhere.exe`. So the library's AOT annotations are clean, and the publish itself could not be completed.
- Running the trial on the managed runtime caught a mistake in the trial program, not in the library:
  comparing a source record to its deserialized copy with `==` fails because a record's generated equality
  uses `EqualityComparer<long[]>.Default`, which is reference equality for an array. The comparison had to be
  field-by-field with `SequenceEqual`. A smoke test that is only compiled is worth little.

### Known Issues and Next

- Native AOT is not verified end to end in this repository. The guarantee offered is the analyzer pass plus
  the annotations on the reflection entry points. Closing it properly needs a machine or CI job with the C++
  workload.
- P3-8 is narrower than first recorded. `TASK_LOG.md` is tracked *and* listed in `.gitignore`; `AGENTS.md` is
  listed there but is not tracked, so it is not a second instance of the same problem. The open question is
  therefore only whether `TASK_LOG.md` should leave the index or leave `.gitignore`.

## 2026-09-25 - Give the project real packaging metadata

### Scope

Fix the first half of the audit's P3-6. `Poly.NBT.csproj` carried only build properties, so `dotnet pack`
produced a package with the default id, a `1.0.0` version the project had never chosen, no description, no
tags, no repository link, and no README. The README's `Implemented / Planned` table also still listed
`ToElement` / `FromElement` as *Planned* while the same file documents them as working.

### Actual Changes

- Added `PackageId`, `VersionPrefix` (`0.1.0`), `Title`, `Description`, `Authors`, `PackageTags`,
  `RepositoryType` and `RepositoryUrl` (read from the `origin` remote), and `PackageReadmeFile`, with a
  `None` item that packs the repository README to the package root.
- No `PackageLicenseExpression`. The repository has no license file, and naming one in the package would
  assert a choice the project has not made; a comment in the project file says to add the two together.
  `VersionPrefix` is below 1.0 on the same principle - nothing has shipped.
- README's `Implemented / Planned` table moves `ToElement` / `FromElement` into the `Implemented` column,
  which is where the rest of the document already treats them.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 209/209 passed.
- `dotnet pack Poly.NBT/Poly.NBT.csproj -c Release`: produced `Poly.NBT.0.1.0.nupkg` with no warnings. The
  package was opened and its `.nuspec` read back: id `Poly.NBT`, version `0.1.0`, the description and tags,
  `README.md` at the package root, the repository URL with the packed commit, and the `PolyType` 1.4.1
  dependency for `net10.0`. The temporary output was removed afterwards.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.

### Known Issues and Next

- The library's own license is still undecided, which is now visible as a missing field in the package
  rather than as silence. Adding a `LICENSE` file and a `PackageLicenseExpression` together is a project
  decision, not a code change.
- The README's opening claim of Native AOT friendliness is still unverified; that is P3-7, the AOT smoke
  project, handled next.
- The remaining P3 item is the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Add an .editorconfig and fix what it reports

### Scope

Fix the audit's P3-5. The repository had no `.editorconfig`, so `dotnet format` fell back to per-machine
defaults and the style could neither be enforced nor reviewed. The format check the earlier commits ran was
therefore checking much less than it appeared to: whitespace and line endings, but not import order, not
unused usings, and not the language and framework-usage rules the project actually follows.

### Actual Changes

- Added a root `.editorconfig` with the rules the code already follows, which makes them enforceable rather
  than incidental: four-space indentation, trimming and a final newline, file-scoped namespaces, usings
  outside the namespace with `System.*` first, predefined type names over framework type names, readonly
  fields, and the null-check shape in use. Rules the code does not follow uniformly are recorded as
  preferences rather than warnings, so the file documents the house style without `dotnet format` rewriting
  working code.
- `end_of_line` is deliberately absent. Pinning it to `lf` was tried first and immediately produced hundreds
  of `ENDOFLINE` errors against the Windows working tree, and pinning it to `crlf` would do the same on a
  Linux checkout. Line endings stay Git's job.
- Turning the rules on surfaced eight real findings, all fixed here: import order in `OptionalConverter.cs`,
  `PrimitiveConverters.cs`, and `RuntimeObjectConverter.cs`; an unused `using System.Globalization` in
  `SnbtNumbers.cs`; an unused `using PolyType.Abstractions` in `NbtSerializer.cs` and `GapCoverageTests.cs`;
  and an unused `using System.Text` in `SnbtWriter.cs` and `SnbtWriterStreamingTests.cs`. Each had outlived
  its last use, and nothing was checking.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 209/209 passed.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed with the new
  configuration, and it reported the eight findings above before they were fixed - which is the evidence that
  the file is being read and not merely present.
- `IDE0005` is raised to a warning on purpose, so an unused using now fails the format check instead of
  surviving indefinitely.

### Known Issues and Next

- `.editorconfig` has no `charset`. The files in the repository are mixed: several begin with a UTF-8 BOM and
  the rest do not. Specifying either value would rewrite half the tree for no behavioral gain, so encoding is
  left alone; normalizing it is a separate decision.
- The remaining P3 items are untouched: package metadata and the `Implemented/Planned` table, an AOT smoke
  project, and the tracked `TASK_LOG.md` decision. README needs no change for this one: `.editorconfig` is its
  own documentation.

## 2026-09-25 - Reject a null DOM payload instead of dereferencing it

### Scope

Fix the audit's P3-4. `NbtString(null!)` and `NbtByteArray(null!)` were constructible, and the same applied to
the other array elements. NBT has no absent value, so a null payload is a caller mistake rather than a state
worth carrying - but nothing stopped one being built, and the failure at write time was different on each
path: `NbtStringCodec.Write` raised `ArgumentNullException`, while `SnbtWriter.AppendElement` fell into its
switch default and evaluated `element.GetType()` on the null, producing a `NullReferenceException` that named
nothing. A `null` element inside a `NbtList` or `NbtCompound` reached the same `NullReferenceException`, while
the binary writer already reported it as `InvalidDataException`.

### Actual Changes

- `NbtString`, `NbtByteArray`, `NbtIntArray`, and `NbtLongArray` reject a null payload in the property
  initializer, so `new NbtString(null!)` and friends throw `ArgumentNullException`. The initializer form is
  what makes this work with a positional record: the initializer reads the primary-constructor parameter, so
  the parameter is not left unread the way it is when the property is declared with a custom accessor.
- `SnbtWriter.AppendElement` gained a `case null:` that throws `InvalidDataException` with the same message the
  binary writer uses, so a null inside a container fails identically on both paths instead of producing a
  `NullReferenceException`.
- `SnbtWriter.AppendString` rejects a null payload with the same exception, which covers a null that arrives
  through a `with` expression.
- `NullDomPayloadsAreRejected` and `ANullElementInsideAContainerIsReportedByBothWriters` pin all of it: the four
  constructors, a null string in a container and at the root, and a null element in a list and a compound, with
  both writers checked.
- README's `Requirements` list records the rule.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 209/209 passed (previously 207).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Two rejected alternatives, kept here so they are not tried again: declaring the property with a custom
  `init` and the C# 14 `field` keyword leaves a positional record's parameter unread (CS8907) and the property
  unassigned (CS9264), and the `{ get; init; } = Value ?? ...` form guards the constructor but not `with`, which
  clones and sets the property without running the initializer.

### Known Issues and Next

- A `with` expression can still put a null payload in place, and there the binary writer still reports
  `ArgumentNullException` from the string codec while the SNBT writer reports `InvalidDataException`. Both are
  real exceptions rather than a `NullReferenceException`, and the two paths disagree only for a state that a
  constructor cannot produce.
- `NbtList` and `NbtCompound` do not scan for null elements, deliberately: the constructors already copy their
  input, and a second pass would add a full traversal to the read path for a mistake the writers already catch.
- The remaining P3 items are untouched: a missing `.editorconfig`, package metadata and the
  `Implemented/Planned` table, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Return an unescaped quoted string as a slice of the input

### Scope

Fix the audit's P3-3. `SnbtLexer.ReadQuotedString` allocated a `StringBuilder` and copied into it one character
at a time for every quoted string, including the common case with no escape sequence at all. The input is
already a `ReadOnlySpan<char>` the lexer holds, so a string that needs no decoding can be sliced straight out
of it.

### Actual Changes

- `ReadQuotedString` scans ahead for the closing quote or the first backslash before it allocates anything. On
  the closing quote it returns `_text.Slice(...)` directly; on a backslash it creates the `StringBuilder`,
  primes it with the plain prefix that was already scanned, and continues with the original loop unchanged.
  The escape handling, the error offsets, and the exception messages are byte-for-byte the same code as before.
- `ReadingPlainQuotedStringsDoesNotAllocateThroughABuilder` pins the fast path with an allocation delta between
  a 1 KB and a 4 KB quoted string, the same shape as the existing read-path allocation tests.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 207/207 passed (previously 206).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- The new guard was checked against a reintroduced regression rather than assumed: with the scan disabled so
  every string takes the builder path, the test reports 4.05 bytes per character and fails against its 2.5
  threshold. With the scan it is under the threshold. The experiment was reverted.
- All existing escape, unterminated-string, and error-offset tests pass, which is what makes the "same code
  path after the first backslash" claim checkable.

### Known Issues and Next

- A string that does contain an escape still copies character by character after the first backslash. That
  could be tightened to skip plain runs between escapes, but the escape case is the minority and the current
  shape keeps the decoder readable.
- The remaining P3 items are untouched: the null-constructible DOM payloads, a missing `.editorconfig`, package
  metadata and the `Implemented/Planned` table, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Read a sign before an omitted integer part as a number

### Scope

Not an audit item. Found while adding the P3-1 tests: `+.5` and `-.5` are legal float literals, but
`IsNumberCandidate` only accepted a sign followed by a *digit*, so `token[1] == '.'` failed the test and the
token fell through to the bare-string branch of the value reader. A document containing `+.5` was therefore
read as the string `"+.5"`, which is a wrong type rather than a rejection - the failure mode that is hardest
to notice.

The report's grammar table lists omitted integer and fractional parts as supported and matches the
implementation, but it only exercises `.1` and `1.`; the signed forms are not covered. `.5` and `5.` work
today because the point is followed by a digit, which is exactly what the check asked for.

### Actual Changes

- `IsNumberCandidate` now also accepts a sign followed by a point followed by a digit, so `+.5` and `-.5`
  reach `ParseDecimal`. Everything downstream already handled them: the omitted-part rule decides whether the
  dialect accepts them, and the normalizer added in the previous commit turns `-.5` into `-0.5`.
- `OmittedFloatPartsAreDialectSpecific` gained `+.5` and `-.5` on both sides of the dialect gate: parsed as
  `NbtDouble` under `v1_21_5`, refused with `SnbtParseException` under `v1_13`, matching `.5` and `5.`
- `QuotesStringsThatStartWithASignOrPoint` gained `-.5` and `+.5`, because the writer has to keep quoting them
  now that the reader treats them as numbers; without the quote the round trip would turn a string into a
  double.
- README's dialect section states the rule.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 206/206 passed. The two tests that changed are the ones above and
  both gained assertions rather than losing any.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- The writer needed no code change: `CanWriteBare` already rejects a leading sign or point before it consults
  the parser, so the quoting decision does not depend on this fix.

### Known Issues and Next

- `-foo` and `.foo` still read as bare strings, which is intentional: they are not literals, so the parser
  recovers the way it always has. Only text that *is* a literal changed meaning.
- The remaining P3 items are untouched: the unconditional `StringBuilder` in `ReadQuotedString`, the
  null-constructible DOM payloads, a missing `.editorconfig`, package metadata and the `Implemented/Planned`
  table, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Drop the temporaries and the linear scan from number parsing

### Scope

Fix the audit's P3-1 and P3-2. Both are on the SNBT number-parse path, so they are one change to one file.
Neither alters behavior; both remove work.

`ParseFloatValue` built its normalized literal through a four-step string pipeline: a `StringBuilder` to strip
digit separators, `ToString`, an `Insert` to add a missing integer part, `+=` to add a missing fractional part,
and a slice to drop a leading `+`. One float literal therefore produced three or four temporary strings before
the runtime parser saw it.

`SplitSuffix` tested the suffix letters with `SuffixLetters.Contains`, a linear scan of a fourteen-character
constant string, once per character of every numeric token.

### Actual Changes

- `ParseFloatValue` now normalizes in a single pass into a `Span<char>`: a stack buffer for an ordinary literal
  and an `ArrayPool<char>` buffer once the literal exceeds 256 characters, returned in a `finally`. The
  rewrite is structural rather than a set of patches - the sign is copied once, a `0` is inserted before a
  leading point as it goes, a trailing point gets its `0` appended, and separators are skipped in the copy loop.
  `double.TryParse` and `float.TryParse` take the span directly, so the normalized form is never a `string`.
  `using System.Text` is gone with the `StringBuilder`.
- `SuffixLetters` and its `string.Contains` are replaced by an `IsSuffixLetter` char pattern, and the constant
  is removed rather than left unused.
- `FloatNormalizationCoversSignsSeparatorsAndLongLiterals` pins the branches the rewrite touches: a leading `+`
  dropped, a trailing point completed, a separator removed, and a literal past the stack-buffer threshold,
  which exercises the rental path and still reports out of range instead of crashing.
- README's `Performance` section gains a bullet recording that a numeric literal now allocates nothing.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 206/206 passed (previously 205).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Behavior-preserving: the pre-existing `.5`, `5.`, `1_000.5f`, and exponent tests pass unchanged. The
  existing `AllowOmittedFloatParts` dialect gate still rejects `.5` and `5.` under `v1_13`, because the
  normalization runs only after that check.

### Known Issues and Next

- Found while writing this change, and out of the audit's scope: `+.5` and `-.5` are legal float literals but
  `IsNumberCandidate` requires a digit immediately after the sign, so both fall through to `NbtString`. That
  silently reads a float as a string. The writer is unaffected, because it already quotes any string starting
  with a sign, so the fix is reader-only. Handled as the next commit.
- The remaining P3 items are untouched: the unconditional `StringBuilder` in `ReadQuotedString`, the
  null-constructible DOM payloads, a missing `.editorconfig`, package metadata and the `Implemented/Planned`
  table, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Tolerate any element type on an empty list

### Scope

Fix the audit's P2-5. The same legal bytes were read differently by the two entry points: `09 08 00 00 00 00`
(an empty `TAG_List` declaring `TAG_String` elements) produced an empty `NbtList` through the DOM but threw
`InvalidDataException: An empty NBT list has an incompatible element type.` through `List<int>`. The audit
offered two directions and a documentation fallback, and left the choice open.

The report's own compatibility matrix settles it. Its `TAG_List` empty-list row states the expected behavior
as "write `TAG_End`, tolerate on read", which is the DOM's behavior, not the typed path's. The bytes are
structurally valid: the element-type field must hold a tag id, but nothing in the format says an empty list
has to declare `TAG_End`. Minecraft normalizes to `TAG_End` on write, and this library does too, so the field
carries no information when the length is zero - there are no elements for it to disagree with. Rejecting it
only turned a writer convention into a reader requirement, and only on the typed path, which is exactly the
inconsistency the DOM never had.

### Actual Changes

- `NbtEnumerableConverter.ReadHeader` lost its `count == 0` branch. The element-type check now runs only when
  `count > 0`, so an empty list accepts any declared element type on the enumerable, mutable-enumerable, and
  parameterized-enumerable paths alike (all three read the header through this one method). A non-empty list
  whose declared type disagrees with the target element type, or that declares `TAG_End`, is still rejected.
- `EmptyListElementTypeIsToleratedOnEveryPath` runs over all four presets. It takes the writer's own empty
  list (which declares `TAG_End`), patches the element-type byte to `TAG_String`, and asserts that the DOM,
  `List<int>`, and `List<string>` all produce an empty collection. Building the bytes from the writer keeps
  the length encoding dialect-correct instead of hardcoding four-byte lengths that a VarInt dialect misreads.
  The same test asserts that a one-element string list still fails as `List<int>`, so the tolerance is pinned
  to the empty case and not to lists in general.
- README gained a `Lists` section stating the rule and the reason, next to the length limits it qualifies.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 205/205 passed (previously 201; the new test is a four-dialect
  theory).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- No existing test had asserted the old strict behavior, so nothing was weakened to make this pass.

### Known Issues and Next

- The DOM still normalizes an empty list's element type to `TAG_End` on write, so a read/write round trip of
  `09 08 00 00 00 00` is not byte-identical. That is a property of the DOM, which has no field to store the
  declared type in, and is now the documented behavior rather than a surprise.
- The P3 series is next: `SnbtNumbers.Decimal` temporary strings, the `SuffixLetters.Contains` scan, the
  unconditional `StringBuilder` in `ReadQuotedString`, null-constructible DOM arrays, a missing
  `.editorconfig`, package metadata, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Document the cost of the convenience conversions and span overloads

### Scope

Fix the audit's P2-3 and P2-7, and close the second half of P2-4. Three public entry points have a cost that
is not visible from their signature, and the audit proposed either changing the design or documenting it. It
proposed the documentation as the zero-risk option and it is the right one here:

- `NbtElementExtensions.ToElement`/`FromElement` (P2-3) convert by serializing to a `MemoryStream` and reading
  the bytes back, so each call is a full serialize plus a full deserialize with an intermediate buffer. That is
  a deliberate single-traversal design, not an oversight, but nothing in the API said so.
- `NbtSerializer.Deserialize(ReadOnlySpan<byte>, ...)` and `DeserializeDocument(ReadOnlySpan<byte>, ...)`
  (P2-7) call `data.ToArray()` unconditionally. The rationale - the readers are stream-based and the BCL has no
  read-only span adapter for `Stream` - is sound, but an overload taking a span reads as though it avoids a
  copy, which it does not.
- The VarInt dialects read through a per-byte `ReadByte` (P2-4). Free on a `MemoryStream`, expensive on an
  unbuffered network stream. Worth stating next to the array path so a caller can tell which is which.

Documenting all three in one place keeps the guidance together instead of scattering it across a page of xmldoc
that a reader has to assemble themselves.

### Actual Changes

- `NbtElementExtensions` gained a type-level `<remarks>` block: the conversion round-trips through the wire
  format and costs a serialize plus a deserialize plus one buffer, the reason (one traversal instead of two
  that have to be kept in step), the cheaper alternative when a stream is already in hand, and that the
  conversion is subject to `MaxDepth` and `MaxCollectionLength` like any other read. Each method got a one-line
  `<remarks>` pointing back at it.
- The two `ReadOnlySpan<byte>` overloads in `NbtSerializer` gained xmldoc: the span is copied into a read-only
  `MemoryStream`, the overload exists for ergonomics rather than to avoid the copy, and a hot loop should keep
  one `MemoryStream` and reset `Position` instead. `DeserializeDocument(ReadOnlySpan<byte>, ...)` uses
  `<inheritdoc>` so the two stay in step.
- README gained a `Performance` section collecting the three facts above and the array path: primitive arrays
  move in one `ReadExactly`/`Write` over `MemoryMarshal.AsBytes` with an `ArrayPool` swap only on a byte-order
  mismatch, and scalars go through `stackalloc`, so a scalar read allocates nothing beyond the string itself.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 201/201 passed.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Documentation-only change: no behavior is altered, and no test was touched.

### Known Issues and Next

- The span overloads still copy. Removing the copy would mean a span-based reader, which is a real change to
  the I/O core rather than a documentation fix, so it is left as documented behavior for now.
- P2-5 remains open: an empty `TAG_List` carries no element type, and the DOM accepts it while `List<int>` does
  not. One behavior has to be chosen or the asymmetry documented.
- The P3 series is untouched: `SnbtNumbers.Decimal` temporary strings, the `SuffixLetters.Contains` scan, the
  unconditional `StringBuilder` in `ReadQuotedString`, null-constructible DOM arrays, a missing `.editorconfig`,
  package metadata, an AOT smoke project, and the tracked `TASK_LOG.md` decision.

## 2026-09-25 - Make the compound key order a documented contract

### Scope

Fix the audit's P2-6. `NbtCompound` backed its entries with a `Dictionary<string, NbtElement>` and the order of
`Keys`, `Values`, and enumeration was therefore an implementation detail - one that both consumers already
depend on, because the SNBT writer emits properties in enumeration order and so does the binary writer. The
current runtime happens to enumerate a `Dictionary` in insertion order while nothing is removed, so the
behavior was right and unpromised.

### Actual Changes

- The backing store is now `System.Collections.Generic.OrderedDictionary<TKey,TValue>`, which documents
  insertion order and keeps O(1) lookup. Both constructors keep their shape, their `StringComparer.Ordinal`
  comparison, and their `ArgumentException` on a duplicate key.
- `NbtCompound` gained a `<remarks>` block stating that enumeration order is part of the contract, naming both
  consumers, and stating the counterpart rule: equality and hashing stay order-independent, so two compounds
  with the same entries in different orders are equal, which is what NBT's unordered-map semantics require.
- New `NbtCompoundOrderTests` pins all three claims: every construction route keeps the given order (including
  the decode path, which builds its own dictionary while reading), order is observable in both the bytes and
  the text while equality and hashing ignore it, and duplicate keys are still rejected.
- README's Requirements list gained the same contract as a bullet.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 201/201 passed (previously 198).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- The audit's ordering probe already showed insertion order on every route; it is now a guarantee instead of an
  observation.

### Known Issues and Next

- A `Dictionary` passed into the constructor still contributes its own enumeration order, which the new test
  says explicitly. The compound does not reorder its input, and a caller who needs a specific order should pass
  the entries in that order rather than rely on the dictionary.
- Equality remains order-independent by design, so `NbtCompound` cannot be used to compare two documents for
  byte-level identity. `Serialize` remains the way to do that.

## 2026-09-25 - Route DOM primitive arrays through the bulk I/O path

### Scope

Fix the audit's P2-2. `NbtElementConverter` wrote `TAG_Int_Array` and `TAG_Long_Array` element by element
through `Numeric.WriteInt32`/`WriteInt64`, while `FixedArrayIO` already implemented the bulk path the strongly
typed converters use: one `Write` over `MemoryMarshal.AsBytes`, plus an `ArrayPool` byte-swap only when the
dialect's byte order disagrees with the machine's. The DOM path is the one a caller reaches through
`NbtDocument`, `Serialize(NbtDocument)`, and the SNBT round trip, so it was the slow one for the same data.

| Path | Before | After |
|:---|:---|:---|
| DOM `int[100k]` write | 2.37 ms/op | 0.24 ms/op |
| typed `int[100k]` write | 0.24 ms/op | 0.24 ms/op |
| DOM `long[100k]` write | 2.52 ms/op | 0.31 ms/op |
| typed `long[100k]` write | 0.30 ms/op | 0.31 ms/op |
| DOM `int[100k]` read | - | 0.13 ms/op |
| typed `int[100k]` read | - | 0.10 ms/op |

### Actual Changes

- `NbtElementConverter`'s `NbtIntArray` and `NbtLongArray` write cases call `FixedArrayIO.WriteInt32` /
  `WriteInt64`, and `ReadIntArray` / `ReadLongArray` call `FixedArrayIO.ReadInt32` / `ReadInt64`. Both helpers
  already branch on the numeric codec, so a VarInt dialect keeps its element-by-element fallback and a
  fixed-width dialect gets one transfer.
- `ReadIntArray` and `ReadLongArray` lost their unused `depth` parameter. Arrays hold no nested tags, so unlike
  `ReadList` and `ReadCompound` they are not part of the recursion; the parameter only looked like it was.
- New `DomAndTypedPrimitiveArraysAgreeOnTheWire` runs over all four presets and asserts that the DOM bytes and
  the strongly typed bytes are identical, that the DOM reads back through both converters, and that the long
  case is skipped where the dialect has no `TAG_Long_Array`. Routing through the bulk path changes byte order
  handling, so this is the invariant the refactor had to preserve.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 198/198 passed (previously 194).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Re-ran the audit's throughput probe in Release, best of three rounds of twenty, on 100,000 elements: the DOM
  path is now indistinguishable from the strongly typed one for both writes and reads, where the audit measured
  roughly 10x and 8x slower.

### Known Issues and Next

- `TAG_Byte_Array` needed no change: its DOM read already went through `StreamIO.ReadExactly` and its write
  through a single `Stream.Write`.
- The audit's remaining P2 items are documentation rather than code: the `ToElement`/`FromElement` cost, the
  compound key order, the `ReadOnlySpan<byte>` copy, and the VarInt byte-at-a-time read on an unbuffered
  network stream.

## 2026-09-25 - Document the SNBT quote-selection rule

### Scope

Close the audit's P1-6 as a documented, deliberate difference rather than a change. The Wiki describes the
quote-selection rule for the `/data get` path, which always quotes and picks the opposite of whichever quote
mark appears first; this writer produces bare strings wherever the grammar allows them, so it is the
bare-where-possible path, and the two rules are not comparable input for input.

| Value | Written | Wiki's `/data get` rule |
|:---|:---|:---|
| `a"b` | `'a"b'` | agree |
| `a'b` | `"a'b"` | agree |
| `a"b'c` | `"a\"b'c"` | `'a"b\'c'` |

### Actual Changes

- README gained a "Quote selection" subsection stating the rule, naming the `/data get` path it differs
  from, and recording that both forms parse to the same value.
- `SnbtWriterTests.PicksTheQuoteCharacterThatNeedsNoEscaping` pins all four cases and asserts the round trip
  of the both-kinds form, so the claim in the README is checked rather than stated.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 194/194 passed (previously 193).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.

### Known Issues and Next

- Deliberately not changed. Adopting the Wiki's rule would mean either always quoting - which loses the
  bare-string output that makes hand-written SNBT readable and that the rest of the writer is built around -
  or applying a rule the Wiki records for a different code path. The difference is textual only.
- Next: the P2 list (per-scalar allocation is done; the DOM array write path, `ToElement`/`FromElement` cost,
  key-order and span-copy documentation remain) and the P3 list.

## 2026-09-25 - Implement the full SNBT string escape set

### Scope

Fix the audit's P1-3. The SNBT grammar lists thirteen string escapes; six were recognized in a double-quoted
string and two in a single-quoted one, so SNBT written by Minecraft or by a person - notably `\s` for a space
- failed to parse.

| Escape | Double-quoted before | Single-quoted before | Now |
|:---|:---:|:---:|:---:|
| `\b` `\f` `\s` | missing | missing | both |
| `\xhh` `\UHHHHHHHH` | missing | missing | both |
| `\n` `\t` `\r` `\\` `\"` | present | missing | both |
| `\'` | missing | present | both |
| `\N{name}` | missing | missing | refused by name |

### Actual Changes

- `SnbtLexer.ReadQuotedString` now applies one table to both quote styles. The grammar does not give
  single-quoted strings a smaller set, and `\s`, `\b` and `\f` are now decoded, as are `\xhh` (two hex digits)
  and `\UHHHHHHHH` (eight).
- The read-side quote branch is gone, so a single-quoted string no longer has a separate, smaller code path -
  which is where the divergence came from in the first place.
- `ReadUnicodeEscape` generalized to `ReadHexEscape(digits, ...)` and accumulating in `long`, because eight hex
  digits do not fit in the `int` the old helper used; `\U` additionally validates the result as a code point,
  rejecting anything above `U+10FFFF` and the surrogate range, then emits a surrogate pair via
  `char.ConvertFromUtf32` when the value is outside the BMP.
- `\N{name}` is refused by name with a dedicated message. A partial name table would be worse than none: it
  would accept `\N{snowman}` and reject `\N{SNOWMAN}` with no way for a caller to tell an unsupported name
  from a misspelled one. Minecraft does not write `\N{name}` either, so the practical loss is nil.
- `SnbtErrorTests.ReportsStringErrors` gained the malformed `\x`, short `\U`, out-of-range `\U`, surrogate
  `\U` and `\N{name}` cases; the case that asserted `'a\nb'` is invalid now asserts the same shape through a
  genuinely unsupported escape (`'a\qb'`), since the escape set is no longer quote-dependent.
- New `ParsesEveryEscapeSequenceInBothQuoteStyles` and `EscapedStringsSurviveAWriteAndReadCycle`.
- README gained a "String escapes" table and a Limitations entry; `SnbtOptions` explains why the escape set is
  not dialect-gated.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 193/193 passed (previously 191).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Audit probe: all twelve escapes now decode in both quote styles, `\U0001F600` produces the two-code-unit
  pair, and `\N{Snowman}` reports `SnbtParseException` at offset 1.

### Known Issues and Next

- The escape set stays dialect-independent on purpose. Escapes arrived with 25w09a, so a strict `v1_13` would
  reject them - but the writer emits them under both dialects, so gating would make its own output unreadable.
  `OutputRoundTripsUnderItsOwnDialect` pins that.
- `\N{name}` remains the one unimplemented escape. Supporting it means shipping Unicode's name database; the
  refusal is explicit so a caller sees a named limitation rather than a generic syntax error.
- Next: the quote-selection rule (P1-6) and then the P2/P3 list.

## 2026-09-25 - Write floating-point literals in Java's shape

### Scope

Fix the audit's P1-5. The writer formatted floats with the runtime's `"R"` format, which differs from Java's
`Double.toString`/`Float.toString` in three systematic ways. None of them is a parse error - Java reads both
forms - but each one is a stable false positive in any diff, checksum, or cache key computed over SNBT
output, which is the only reason to care about the text at all.

| Value | Before | After | Java |
|:---|:---|:---|:---|
| `1e20` | `1E+20d` | `1.0E20d` | `1.0E20` |
| `1e-20` | `1E-20d` | `1.0E-20d` | `1.0E-20` |
| `1.2345678901234568e17` | `1.2345678901234568E+17d` | `1.2345678901234568E17d` | same |
| `1e7` | `10000000.0d` | `1.0E7d` | `1.0E7` |
| `1e-4` | `0.0001d` | `1.0E-4d` | `1.0E-4` |
| `9999999.0` | `9999999.0d` | `9999999.0d` | same |

### Actual Changes

- Rewrote `AppendFloatLiteral` around a single normalization: any literal - `"R"` plain or `"R"` exponential -
  is reduced to its significant digits plus the position of its decimal point, and the output form is chosen
  from that value rather than from the shape the runtime happened to pick. `AppendPlainDecimal` is gone; the
  plain and exponential branches now share the same digits.
- Three rules changed to match Java: the mantissa always keeps a decimal point and at least one digit after
  it (`1.0E20d`, `100.0d`, `0.0d`); the exponent carries no `+` and no leading zeros; and `E`-notation is
  used outside `[10^-3, 10^7)` where `"R"` stays plain until `10^15`.
- `AppendFloatLiteral` is now documented with those rules and with the one difference that remains.
- `SnbtFloatFormatTests` adds three tables of Java-documented outputs for the same bit patterns (24 doubles,
  13 floats, and the classic dialect's expansion of the same digits), a round-trip sweep over both dialects,
  and one test that records the residual difference as an executable statement.
- README's "Dialect effect on output" section gained the rule table and the same caveat.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 191/191 passed (previously 143).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Swept the probe set through both dialects: every value round-trips exactly, including `double.MinValue`
  whose classic-dialect expansion is 309 digits long and `double.Epsilon` whose expansion is 324.

### Known Issues and Next

- The digits still come from the runtime's shortest round-trippable form, which is provably not always the
  digits Java picks. For the smallest subnormals Java prints `4.9E-324` for `Double.MIN_VALUE` and `1.4E-45`
  for `Float.MIN_VALUE`, while the runtime's shortest forms are `5E-324` and `1E-45`. Reaching digit parity
  would mean reimplementing Java's `FloatingDecimal`, and the two texts parse to the same value, so the
  difference is recorded in a test and in both doc surfaces instead of being chased.
- Consequently the guarantee is "the same shape in every ordinary case", not "byte-identical to the game in
  every case". Anyone comparing SNBT text across implementations should compare parsed values.
- This is a behavior change for `v1_21_5` output in `[1e7, 1e15)` and `[1e-4, 1e-3)`, which now use an
  exponent. The classic dialect is unaffected: it expands the same digits into a plain decimal either way.

## 2026-09-25 - Quote SNBT strings that start with a sign or a point

### Scope

Fix the audit's P1-4, a write-side interoperability bug. SNBT reserves a leading digit, sign, or point for
numbers and `CanWriteBare` only rejected a leading digit, so `new NbtString("-foo")` was written bare as
`-foo`. Minecraft's tokenizer tries a numeric parse first and reports an error rather than falling back to a
bare string, so `{a:-foo}` is not valid SNBT. The library's own parser *does* fall back, which is exactly why
`WrittenStringsRoundTrip` was green and the bug stayed hidden.

| Value | Before | After |
|:---|:---|:---|
| `-foo` | `-foo` | `"-foo"` |
| `+foo` | `+foo` | `"+foo"` |
| `.foo` | `.foo` | `".foo"` |
| `a-b`, `a+b`, `a.b` | bare | bare - only the first character is reserved |

### Actual Changes

- `CanWriteBare` now rejects a leading `-`, `+`, or `.` as well as a leading digit. `+` is included for the
  same reason as `-`: the reader accepts `+1`, so a leading `+` begins a number.
- New `QuotesStringsThatStartWithASignOrPoint` covers the three newly quoted forms, `-1.5`, and the negative
  cases that must stay bare. The previous test list already contained `"-foo"`, `".foo"` and `"a+b"`, but
  asserted them against this library's own lenient parser instead of the game's grammar.
- README's string-quoting paragraph now states the full reserved-first-character set and says why a round
  trip through this library cannot detect the problem.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 143/143 passed (previously 142).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `WrittenStringsRoundTrip` still passes, so nothing that used to be written bare has become unreadable.

### Known Issues and Next

- The parser deliberately stays more lenient than the game and still falls back to a bare string where
  Minecraft reports an error. Tightening it would reject input that is unambiguous here, and the writer no
  longer produces the shape that made leniency matter.
- Next is P1-5, the floating-point text shape.

## 2026-09-25 - Report over-wide VarInts as bad data

### Scope

Fix the audit's P2-4. Minecraft's VarInt is 7 bits per group, so five groups carry 35 bits and ten carry 70.
Both widths can therefore be over-encoded by a hostile or corrupt document, and the reader did the wrong
thing with the extra bits in both cases.

| Payload | Tag | Before | After |
|:---|:---|:---|:---|
| `03 FFFFFFFF7F` | `TAG_Int` | `OverflowException` from the fixed-width cast | `FormatException` |
| `04 FF x9 7F` | `TAG_Long` | extra bits dropped silently, wrong value returned | `FormatException` |

### Actual Changes

- `VarInt.ReadUInt32` no longer casts with `checked`. The value is compared against `uint.MaxValue` and
  rejected as `FormatException` when it does not fit. The old `OverflowException` reads as a library fault
  when the actual fault is the input.
- `VarInt.Read` now checks each group against the bits the target width can still hold before shifting it in.
  The 32-bit case was already covered by the read returning `ulong`, but the 64-bit case shifted the tenth
  group by 63 and discarded everything above bit 0 silently, so an over-wide `VarLong` used to decode to a
  fabricated value instead of failing.
- Documented the class: every malformed shape raises `FormatException`.
- New `VarIntsWiderThanTheTargetAreRejectedAsBadData` covers both widths, and asserts that the widest *legal*
  encoding of each width still decodes - to `int.MinValue` and `long.MinValue`, which is what ZigZag makes of
  an all-ones payload.
- Added a README "Malformed input" section stating the exception contract the whole reader follows:
  `FormatException` for structurally invalid bytes, `InvalidDataException` for bytes that break a configured
  rule or a shape constraint, and `EndOfStreamException` for truncation.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 142/142 passed (previously 141).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Audit probe 5: `03FFFFFFFF7F` now reports `FormatException` instead of `OverflowException`; the valid
  encodings around it are unchanged.

### Known Issues and Next

- The `long` case now rejects rather than truncating, which is a behavior change for anyone who relied on the
  old silent truncation. That was never a defined behavior and no caller can have wanted the fabricated
  value, so it is treated as a bug fix rather than a break.
- Still to come from the audit: the SNBT escape set and bare-string rules (P1-3, P1-4), the float exponent
  format (P1-5), and the P2/P3 list.

## 2026-09-25 - Always write the root name field in a named dialect

### Scope

Fix the audit's P1-2 and the interoperability bug behind it. `Serialize` skipped the root-name field when the
caller passed an empty name, but the field is part of the root header rather than an optional extra: Java's
`NbtIo` reads it unconditionally. An empty name therefore produced a document only this library could read
back, and even that needed a special read flag.

| Dialect | Call | Before | After |
|:---|:---|:---|:---|
| `JavaEdition` | `SerializeUsingReflection(42, "")` | `03 0000002A` | `03 0000 0000002A` |
| `JavaEdition` | round trip of the above | `EndOfStreamException` | `42` |

### Actual Changes

- Split the header write out of `SerializeRoot` into `WriteRootHeader`, which writes the tag byte and then the
  name field exactly when `RootTagNaming` is `Named`. An empty name is now written as an empty name (`00 00`).
  Whether the field exists became purely a dialect property; the `rootTagName` argument no longer controls the
  layout, only its content.
- Documented the enum accordingly - `Named` is "tag, name, payload" (Java files, Bedrock files), `Omitted` is
  "tag, payload" (both network protocols) - and documented the read-side `rootNameOmitted` parameter, which is
  now explicitly an opt-in for non-standard input rather than a peer of the dialect setting.
- Two internal bugs fell out of the same root cause, both in the DOM bridge:
  - `ToElementInternal` read only the tag byte and then handed the stream to the converter, so in a named
    dialect the converter started on the name's length bytes and saw `TAG_End`. Every `ToElement` on a named
    preset silently returned an empty compound. It now goes through `ReadRootHeader`.
  - `FromElementInternal` wrote a nameless header and read it back with `rootNameOmitted: true`. It now uses
    the same `WriteRootHeader`/`ReadRootHeader` pair as the public API, so the bridge and the wire format
    cannot drift apart again.
- Four byte-exact tests updated to include the two-byte empty name
  (`JavaCompoundHasExpectedBytes`, `RootNamesAndTrailingDataFollowDocumentedContracts`,
  `ReadOnlyWriteOnlyAndMaximumStringBoundaries`), and `DomNestedRoundTrips` no longer needs the read flag.
- New tests: `AnEmptyRootNameIsWrittenAsAnEmptyNameField` pins the exact bytes and round-trips through both the
  typed and the document API; `AnOmittedRootNamingWritesNoNameFieldWhateverTheArgumentSays` pins that an
  omitted dialect ignores the name argument entirely.
- READMEs "Root name" section rewritten to state the rule and show both dialects.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 141/141 passed (previously 139).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Audit probe 1 now reports `0300000000002A` and `round trip OK -> 42`; it previously reported
  `030000002A` followed by `round trip THROWS EndOfStreamException`.

### Known Issues and Next

- The read-side `rootNameOmitted` parameter is kept. It now has no producer inside the library, but it remains
  the only way to read NBT written by an implementation that omitted the name in a named dialect, and removing
  a public parameter is a breaking change that the audit did not ask for.
- `ToElement`/`FromElement` remain round-trips through a `MemoryStream` rather than a direct DOM walk. The
  behavior is now correct; the cost is still on the P2 list.

## 2026-09-25 - Bound declared collection and string lengths

### Scope

Close the second input-driven crash class the audit found, and the more dangerous of the two. Every length
prefix in the wire format - the element count of a collection, the encoded byte length of a string - is read
and then handed straight to an allocation, so a handful of bytes could demand two gigabytes before one payload
byte was read. The audit measured `OutOfMemoryException`, which a host cannot pre-empt.

| Dialect | Path | Trigger | Before | After |
|:---|:---|:---|:---|:---|
| Java network | `TAG_Byte_Array` | `07 7FFFFFFF` (5 bytes) | `OutOfMemoryException` | `InvalidDataException` |
| Java network | `TAG_List` of bytes | `09 01 7FFFFFFF` (6 bytes) | `OutOfMemoryException` | `InvalidDataException` |
| Bedrock network | `TAG_Int_Array` | `0B FEFFFFFF0F` (6 bytes) | `OutOfMemoryException` | `InvalidDataException` |
| Bedrock network | `TAG_String` | `08 FFFFFFFF0F` (6 bytes) | `OverflowException` | `InvalidDataException` |

### Actual Changes

- Added `NbtOptions.MaxCollectionLength` with a documented zero sentinel, `DefaultMaxCollectionLength = 1 << 24`,
  and set it explicitly on all four presets, mirroring how `MaxDepth` is declared. It caps both a collection's
  element count and a string's encoded byte length, on reading and on writing.
- Rewrote `NbtLengthCodec` around that limit. The base class now owns a single private `Validate` gate, and
  `FixedLengthCodec`/`VarIntLengthCodec` only supply the dialect-specific read and write primitives. Both
  `ReadCollectionLength(Stream)` and `ReadStringLength(Stream)` funnel through the gate before returning, so
  every allocation site in the library inherits the check rather than repeating it.
- The `*Core` members return `long` instead of `int`. A VarInt length is a 32-bit *unsigned* field, so
  `uint.MaxValue` is representable on the wire; the previous `checked((int)VarInt.ReadUInt32(stream))` surfaced
  that as an `OverflowException`, which reads as a library bug rather than as bad input. Widening to `long` lets
  the gate reject it as out-of-range data.
- The write side is gated too. A caller-built collection above the limit throws the same
  `InvalidDataException` instead of emitting a document the reader would refuse.
- `NbtSerializer` passes the resolved limit into both codecs and rejects a negative `MaxCollectionLength` at
  construction, next to the existing negative-`MaxDepth` check.
- New `LengthLimitTests` (nine tests): each preset carries the default; a byte-array, list, IntArray and string
  length bomb is rejected with a message naming the option and the offending length; a truncated collection is
  still reported as `EndOfStreamException` rather than as an oversized one; the limit is configurable and zero
  selects the default; the write path is gated; a negative limit is rejected.
- Documented the limit in the `MaxCollectionLength` remarks and in a README section next to "Nesting depth",
  including what it does *not* cover.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: 139/139 passed (previously 130).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- Re-ran the audit's tampered-length probe against the new build. All three payloads that used to report
  `OutOfMemoryException` now report `InvalidDataException`:
  `077FFFFFFF`, `077FFFFFFF010203`, `09017FFFFFFF`.
- No cost on the success path: `Validate` is a compare and a branch, and the DOM allocation probes were
  unchanged (200-int document still 912 bytes).

### Known Issues and Next

- `1 << 24` is a judgement call, not a Minecraft-derived number. It admits every realistic document - a whole
  16x16x16 chunk section is a few thousand elements - and keeps the worst case for a hostile `TAG_Long_Array`
  around 134 MB instead of unbounded. It is configurable in both directions.
- The limit is *per collection*, so it does not bound a document's total size; a broad tree of individually
  legal collections still adds up. Minecraft's `NbtAccounter` accumulates a running total instead. That is
  documented on the property and in the README, with the advice to bound the input stream as well. A real
  total-accounting mode remains open work.
- Deliberately no "compare the declared length against the bytes remaining" pre-check, which the audit also
  suggested. For a seekable stream it would reject a liar one allocation earlier, but it would make the
  failure mode depend on `CanSeek`, and a non-seekable stream cannot do it at all. Truncation therefore keeps
  its own answer (`EndOfStreamException` from `ReadExactly`), which is pinned by a test.

## 2026-09-25 - Clear nullable warnings in the allocation tests

### Scope

Hygiene follow-up to the per-scalar buffer work: the new allocation tests read the result of
`NbtSerializer.Deserialize` and immediately touched `.Length` or `.Count`, but that method returns `T?`, so the
tree was left with six CS8602 warnings. The build is supposed to be warning-free.

### Actual Changes

- `AllocationTests`: applied the null-forgiving operator at the six call sites. The deserialized value is only
  consumed to stop the reads from being optimized away, and the run would fail at the point of use if the value
  really were null, so no test semantics changed.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors (was 6 warnings).
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: 130/130 passed.

### Known Issues and Next

- None. Next is the length-prefix limit from the audit's P0 list.

## 2026-09-25 - Bound NBT and SNBT nesting depth

### Scope

Close the crash class the audit found: the readers and writers recurse once per nesting level, and the level
count comes straight from the input, so a few kilobytes of crafted bytes killed the process with an
uncatchable `StackOverflowException`.

| Path | Trigger | Before | After |
|:---|:---|:---|:---|
| Binary read | `09 09 00000001` x 3000 | Stack overflow at 3,000 levels (15 KB) | `InvalidDataException` |
| SNBT parse | `[[[[...1...]]]]` at 3,000 levels | Stack overflow (6 KB) | `SnbtParseException` with an offset |
| Serialize | user-built 5,000-deep `NbtList` | Stack overflow | `InvalidDataException` |

### Actual Changes

- Added `NbtOptions.MaxDepth` and `SnbtOptions.MaxDepth`, both defaulting to 512 through a documented zero
  sentinel, and set them explicitly on every preset. `NbtSerializer` rejects a negative value at construction.
- Threaded a `depth` parameter through the whole converter surface: `NbtConverter<T>`, every converter in
  `PrimitiveConverters`/`CollectionConverters`/`ObjectConverters`/`OptionalConverter`/`EnumConverter`/
  `RuntimeObjectConverter`/`NbtElementConverter`, `NbtPropertyConverter`, and `NbtSerializer.SkipPayload`.
  `NbtSerializer.Descend(depth)` increments the level and enforces the limit in one place.
- Argument carrying is deliberate. Converters are cached in a `MultiProviderTypeCache` and shared across
  threads, so a depth counter stored as a field would be visible to concurrent serializations - fixing one bug
  by introducing a race. As a parameter it is per-call by construction.
- `SkipPayload` is bounded too: it recurses over tag types read from the stream, so it was reachable by the same
  input as the main reader.
- The SNBT parser reports the offending bracket's `Offset`; the writers and the binary reader report
  `InvalidDataException`. Every message names the option to raise.
- `EveryPresetCarriesTheDefaultLimit`, `ReadingDeeplyNestedListsFailsInsteadOfOverflowingTheStack` (20,000
  levels), `ReadingDeclarativelyDeepTypesIsAlsoBounded` (`Dictionary<string, object>`), `WritingDeeplyNested
  ListsFailsInsteadOfOverflowingTheStack`, `ParsingDeeplyNestedSnbtFailsWithAnOffset`, a configurable-limit
  test, and a negative-limit test.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 130/130 passed (previously 123).
- Re-ran the audit probes that used to kill the process; all now report the bounded exception and exit
  normally instead of dying:
  `deep-read 20000`, `deep 20000` -> `InvalidDataException`; `deep-snbt 20000` -> `SnbtParseException`.
- Cost when the limit is not reached: none. The depth is an `int` argument and no converter stores it.

### Known Issues and Next

- Every value counts towards the level, the innermost scalar included, so `[[[1]]]` is four levels. That is
  documented in both property remarks and the README; it keeps one uniform rule instead of a second rule for
  container-only counting.
- 512 is a judgement call, not a Minecraft-derived number: Minecraft bounds allocation through `NbtAccounter`
  rather than nesting. The value is configurable for callers that legitimately nest deeper.

## 2026-09-25 - Make NbtOptions value semantics honest

### Scope

Fix the one real correctness bug found by the audit: `NbtOptions` compared unequal to itself.

`OptimizePrimitiveListsToArrays` was backed by a tri-state byte (0 or 1 meant "unset or true", 2 meant "false")
so that `default(NbtOptions)` would read `true`. That worked for exactly one case and broke record equality for
every other: two instances with identical property values had different hash codes and failed `Equals` and `==`.

### Actual Changes

- Replaced the tri-state byte with a plain `bool`. Equality, `GetHashCode`, `==` and `with` are now the
  compiler-generated ones and agree with the properties by construction.
- Declared `OptimizePrimitiveListsToArrays = true` explicitly on `JavaEdition` and `BedrockEdition`, which
  previously inherited it from the sentinel. `JavaNetworkEdition` and `BedrockNetworkEdition` derive from those
  through `with`, so all four presets keep their previous behavior.
- Documented what `default(NbtOptions)` actually is: a coherent dialect rather than a preset - big-endian,
  Modified UTF-8, fixed-width numbers, named root, with both boolean capabilities off.
- `RequestedBehaviorTests`: replaced the test that pinned the sentinel with one that asserts every preset
  enables the optimization, added `OptionsCompareTheWayTheirPropertiesRead` (equality, hash codes, `==`,
  dictionary key, `with`) and `DefaultOptionsDescribeACoherentButUnoptimizedDialect`, which round-trips `byte[]`
  and `long[]` through a `default(NbtOptions)` serializer and asserts they degrade to `TAG_List`.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 123/123 passed.
- Presets are byte-for-byte unchanged, so no wire format moved. The only visible change is that
  `default(NbtOptions)` and `new NbtOptions()` now report `OptimizePrimitiveListsToArrays == false`; that is the
  point of the fix, and the new test documents it.

### Known Issues and Next

- `default(NbtOptions)` remains constructible and describes a dialect nobody uses. The README continues to steer
  callers to the presets, which is the right consumer contract.

## 2026-09-25 - Avoid per-scalar buffers in the wire codecs

### Scope

Remove the per-scalar allocation in the read path. `StreamIO.ReadExactly(Stream, int)` allocated a fresh
`byte[]` on every call, and every scalar in the wire format went through it, so decoding one 4-byte `int`
produced 24 bytes of garbage; `NbtStringCodec.Read` did the same for every string.

### Actual Changes

- `StreamIO` gained `stackalloc`-backed helpers for the six integer reads (16/32/64-bit, both byte orders) plus
  a `Span<byte>` overload of `ReadExactly`. The array-returning overload stays, because byte arrays genuinely
  need an owned buffer and it is now the only allocation left in the read path.
- `BigEndianNumericCodec`, `LittleEndianNumericCodec` and `VarIntNumericCodec` now delegate to those helpers;
  all three are allocation-free for reads. The three `FixedArrayIO` fast paths already bypassed them.
- `NbtStringCodec.Read` reads through a 256-byte stack buffer and falls back to `ArrayPool<byte>.Shared` above
  that, then decodes straight from the `ReadOnlySpan<byte>` (the three decoders already accepted spans).

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 121/121 passed. The 118 pre-existing tests pass untouched, which is
  the point: no public API and no wire format changed.
- Measured on the DOM path (Release): reading `int[200]` fell from 7,344 to 912 bytes per document, i.e. from
  32.7 to 0.6 bytes of garbage per 4-byte integer. A 200-entry compound of ints fell from 127.9 to 95.9 bytes
  per key/value pair; the remainder is the `NbtInt` values and key strings the DOM has to own.
- `AllocationTests` pins the new behavior with delta-based assertions. It diffs two payload sizes so the fixed
  PolyType converter-cache lookup (about 448 bytes per `Deserialize` call, unrelated to the codecs) cancels
  out. Measured per-element cost is now exactly the result array - 4 bytes per `int`, 2 bytes per character.

### Known Issues and Next

- The writer's `Utf8WithEscapes.Encode` and `ModifiedUtf8.Encode` still build a `byte[]` per string. That is the
  output buffer rather than throw-away garbage, so it was left alone.
- A single `Deserialize` call allocates roughly 448 bytes in the PolyType `MultiProviderTypeCache` lookup. That
  is a per-call, not per-element, cost inside PolyType and is worth a separate look if hot single-scalar loops
  matter.

## 2026-09-25 - Stream SnbtWriter output to TextWriter

### Scope

Make the `TextWriter` overloads honor a streaming contract: writing a document must no longer build the
complete string first.

### Actual Changes

- Replaced the `StringBuilder` implementation with a recursive `AppendElement(TextWriter, ...)`. Every
  bracket, separator, scalar, and type suffix reaches the target writer as soon as it is produced.
- Integers are formatted into a `stackalloc char[24]` buffer with `long.TryFormat` and the invariant culture,
  so the result never depends on the culture of the caller's `TextWriter`.
- Floats and doubles are formatted into `stackalloc char[32]` buffers. Exponent expansion streams as well:
  digits are written directly and leading or trailing zero runs come from a shared 400-character padding, so
  `v1_13` output no longer allocates an intermediate literal.
- Strings are written character by character, emitting escape sequences as they are encountered.
- Kept the `string` overloads as convenience wrappers over a `StringWriter`. Every public signature and the
  produced text are unchanged, which is why the pre-existing writer tests passed untouched.
- Added `SnbtWriterStreamingTests` using a recording `TextWriter` that captures every write: the document is
  reassembled from many separate writes, no single write holds the whole document, a scalar arrives as digits
  and suffix separately, and an expanded decimal never appears as one literal.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 118/118 passed (previously 114).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- `AppendString` issues one write per character for quoted strings. That follows directly from the streaming
  contract; batching would be faster but would reintroduce a per-string allocation.

## 2026-09-25 - Use ReadOnlySpan<char> in SNBT parser and lexer

### Scope

Replace the parser's string-based input path with `ReadOnlySpan<char>`, so scanning a document needs neither
a string copy of the input nor an allocation per bare token.

### Actual Changes

- `SnbtLexer` is now a `ref struct` holding a `ReadOnlySpan<char>` instead of a `string`. A struct copy would
  not share the cursor, so every `SnbtParser` method that reads from it now takes it by `ref`. This
  propagation was not part of the original plan and is required for correctness rather than style.
- `SnbtLexer.ReadBareToken` returns a slice of the input. `NbtString` values, compound keys, and `uuid()`
  arguments materialize a string only where the DOM requires one, so numbers, booleans, and operations are
  scanned without allocating.
- `SnbtParser.Parse` and `ParseDocument` take `ReadOnlySpan<char>`. The `string` overloads were dropped: a
  `string` argument binds through the built-in implicit conversion, which is also what removes the
  `Parse(null)` overload ambiguity. The `TextReader` overloads remain for streams.
- `SnbtNumbers` follows the lexer onto spans for every token parameter. Its float path still builds a
  cleaned string before `TryParse`, because stripping underscores and normalizing a bare `.` needs a
  transformation; only the signatures changed there.
- Added `ParsesFromASpanThatIsNotAString`, which parses from a `char[]` span to prove the span path is not a
  string in disguise. Existing `Parse("...")` call sites compile unchanged via the implicit conversion.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 114/114 passed (previously 113).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.

## 2026-09-25 - Accept negative radix literals in SNBT

### Scope

Make a leading minus sign select the signed reading of a hexadecimal or binary literal instead of being
rejected as "the unsigned literal cannot be negative".

### Actual Changes

- `SnbtNumbers.ParseBased` now passes `unsignedSuffix ?? !negative` to `StoreInteger`. Radix literals still
  default to unsigned, but `-0xFF` is the signed `-255`. The default for decimal literals is unchanged.
- Kept an explicit signedness suffix authoritative, which preserves the documented range semantics:
  `-0xFFub` is still rejected because an unsigned literal cannot be negative, and `-0xFFsb` is rejected
  because -255 does not fit a signed byte. Byte-sized hex values remain expressible, e.g. `-0x11sb` is -17,
  which matches the wiki rule that byte-sized hex literals must carry a signed suffix and that the suffix
  only narrows the parsed range.
- Added `ParsesNegativeRadixLiterals` covering `-0xFF`, `-0b101`, `-0xbadL`, `-0x80000000`,
  `-0x8000000000000000L`, `-0x11sb`, and `+0xFF`, plus error assertions for `-0xFFub`, `-0xFFsb`, and
  `-0x80000001`.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 113/113 passed (previously 112).

## 2026-09-25 - Fix SnbtOptions documentation for version provenance

### Scope

Correct the `SnbtOptions` documentation, which attributed every flag to 25w09a. Each flag was verified
against the Minecraft Wiki history tables for the NBT format and the SNBT format, plus the Mojang 1.21.5
release notes.

### Actual Changes

- Rewrote the type remarks: the 1.21.5 text format arrived across three snapshots rather than one -
  25w04a, 25w09a, and 25w10a - so the flag set is coarser than the timeline. A 25w04a dialect, which
  accepts heterogeneous lists but none of the numeric extensions, cannot be expressed by the two presets.
- Recorded the introducing snapshot on each flag: heterogeneous lists 25w04a; trailing commas, scientific
  notation, `0x`/`0b` prefixes, omitted float parts, underscores, signedness suffixes, and the extended
  string escapes 25w09a; `bool()` and `uuid()` 25w10a.
- Documented that the wiki history records no introduction version for the `true`/`false` literals, instead
  of asserting one.
- Corrected the preset description of `v1_13` from "classic 1.13", which implied a 1.13-era grammar, to the
  pre-1.21.5 dialect it actually models.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 112/112 passed.

## 2026-09-25 - Remove unused SnbtNumbers.TryParseInteger

### Scope

Remove dead code. `SnbtNumbers.TryParseInteger` was the only member with no call site.

### Actual Changes

- Deleted `SnbtNumbers.TryParseInteger`. A repository-wide search confirmed nothing referenced it; typed
  arrays read their elements through `SnbtParser.ReadArrayElement`, which projects the result of
  `TryParse` directly.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 112/112 passed.

## 2026-09-24 - Honor the SNBT dialect when writing

### Scope

Make `SnbtWriter` aware of the target dialect so classic (`v1_13`) text can be produced, resolving the
round-trip limitation recorded in the previous entry. Scope was limited to the writer and its tests; the
parser and `SnbtOptions` are unchanged.

### Actual Changes

- Added `SnbtOptions` overloads to every `SnbtWriter.Write` entry point (string- and `TextWriter`-based,
  element- and document-based), with the parameterless overloads delegating to `SnbtOptions.v1_21_5`.
- Added internal exponent expansion: when `AllowScientificNotation` is disabled, the `"R"` rendering is
  rewritten as an equivalent plain decimal literal by moving digits instead of re-formatting the value,
  preserving shortest-round-trip fidelity. Applies to `float` and `double`, including subnormals.
- Kept string quoting dialect-independent and conservative, documenting the reason: the classic dialect
  rejects a number-like token such as `.5` as malformed rather than reading it as a bare string, so
  quoting must follow the most permissive dialect. An earlier attempt to make quoting dialect-aware was
  reverted after a test proved the classic parser throws on bare `.5`.
- Documented in the type remarks which flags can affect output and that a heterogeneous `NbtList` cannot
  be represented in a dialect without `AllowHeterogeneousLists`.
- Added `SnbtWriterDialectTests` covering exponent-free output for extreme values, round-trips under both
  dialects, conservative quoting, and the `TextWriter` overloads.
- Updated `README.md` with a "Dialect effect on output" section.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: 112/112 passed (previously 106).
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- `double.Epsilon` and other subnormals expand to literals over 300 characters under `v1_13`; correct but
  verbose. A shorter form would require a dialect-specific literal the classic grammar does not accept.
- Supersedes the `SnbtWriter` bullet of the previous entry; the remaining known issues there still stand.

## 2026-09-24 - Add SNBT parser and writer

### Scope

Add `Poly.NBT.Snbt`, a text-only conversion layer between the SNBT format and `NbtElement`, with a
complete lexer, parser, and writer plus the `v1_13` and `v1_21_5` dialect presets.

### Actual Changes

- Added `SnbtOptions` (`readonly record struct`) with the nine 1.21.5 extension flags and the
  `v1_13` / `v1_21_5` presets; no `default` preset is provided.
- Added `SnbtParseException : FormatException` carrying the zero-based character `Offset`.
- Added `SnbtLexer` for character scanning and quoted-string decoding using the dialect-independent
  escape set (`\"`, `\\`, `\n`, `\t`, `\r`, `\uXXXX`); single-quoted strings allow only `\'` and `\\`.
- Added `SnbtNumbers` (two partial files) covering decimal, hexadecimal, and binary literals,
  underscores, `E` notation, omitted float parts, `NaN`/`Infinity`, signedness suffixes, range
  checks, and overflow rejection; the `i`/`I` integer suffix is rejected.
- Added `SnbtParser` with `Parse`/`ParseDocument` overloads for `TextReader` and `string`, a
  parameterless modern-dialect path, compound/list/array parsing, duplicate-key rejection, the
  `bool(...)` and `uuid(...)` operations, and typed arrays that ignore mismatched element suffixes.
- Added `SnbtWriter` with bare-string-first quoting, `"R"` invariant floating-point formatting,
  type suffixes, insertion-ordered compounds, and compact single-line output.
- Added 41 SNBT tests across `SnbtParserTests`, `SnbtDialectTests`, `SnbtWriterTests`, and
  `SnbtErrorTests`; the suite now contains 106 tests.
- Documented the SNBT surface and its dialect matrix in `README.md`.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 106/106 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- The 1.21.5 number format is implemented from the published grammar but has not been diffed against
  a live Minecraft build; the `0xA3sb` example on the Minecraft Wiki exceeds the signed byte range
  and is rejected here, matching the documented `253sb` rejection.
- `SnbtWriter` has no dialect parameter, so `v1_13` cannot re-read its own output for values such as
  `1E+20d`, which that dialect disallows.
- SNBT arrays accept only integer-typed literals; fractional literals inside `[B; ...]` are errors.

Planned commit subject: `Add SNBT parser and writer`

## 2026-09-19 - Add object and DOM bridge helpers

### Scope

Add source-generated, explicit-shape, and reflection entry points for converting between .NET object graphs and `NbtElement` trees.

### Actual Changes

- Added internal serializer bridge methods that reuse the configured value converters and existing DOM converter through an in-memory NBT payload.
- Added C# 14 extension members for explicit `ITypeShape<T>`, `IShapeable<T>`, and reflection-based conversion.
- Added coverage for all four presets, all three API paths, null handling, and incompatible target tags.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 65/65 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- Conversion intentionally materializes an in-memory NBT payload between the object converter and DOM converter.
- The pre-existing `Poly.NBT/Dom/NbtElement.cs` modification remains outside this task commit.

Planned commit subject: `Add ToElement and FromElement DOM bridge helpers`

## 2026-09-19 - Document the new public API contracts

### Scope

Update the README for explicit root names, enum mapping, DOM array helpers, and root-aware documents.

### Actual Changes

- Added explicit-root examples for shape-based and reflection serialization.
- Documented enum-to-NBT integer tag mapping, including unsigned bit preservation.
- Added `NbtDocument` and `NbtList.TryToArray` examples and behavior notes.
- Removed completed enum and DOM convenience items from future-facing text while retaining the unsupported Union note.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 58/58 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- The pre-existing `Poly.NBT.slnx` modification remains outside this task commit and prevents a literally clean worktree.

Planned commit subject: `Document enum mapping, NbtDocument, DOM helpers, and explicit root names`

## 2026-09-19 - Add root-aware NbtDocument APIs

### Scope

Pair a DOM root element with its root tag name and preserve that name during document-level reads.

### Actual Changes

- Added immutable `NbtDocument` with `RootTagName` and `RootElement` properties.
- Added stream and buffer serializer overloads for writing and reading documents through the existing DOM converter.
- Preserved stream trailing-data behavior, buffer strictness, and omitted-root preset behavior.
- Added named-root, omitted-root, and trailing-data tests.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 58/58 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- Deserializing formats that omit the root name returns `string.Empty` because no name exists on the wire.
- The pre-existing `Poly.NBT.slnx` modification remains outside this task commit.
- Continue with README documentation after verification.

Planned commit subject: `Add NbtDocument for root name and element pairing`

## 2026-09-19 - Add NbtList primitive array helpers

### Scope

Add explicit DOM conveniences for converting homogeneous scalar lists to CLR arrays.

### Actual Changes

- Added `NbtList.TryToArray` overloads for byte, signed byte, short, int, long, float, double, and string arrays.
- Return `false` with a null output for mismatched element tags and support typed empty-array conversion.
- Added tests for every overload and failure behavior.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 55/55 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- The helpers intentionally do not perform numeric coercion between different NBT tag types.
- The pre-existing `Poly.NBT.slnx` modification remains outside this task commit.
- Continue with `NbtDocument` after verification.

Planned commit subject: `Add TryToArray helpers on NbtList`

## 2026-09-19 - Map enums through their underlying integers

### Scope

Define the default NBT representation for CLR enums without changing standalone unsigned scalar support.

### Actual Changes

- Added an enum converter that maps 8-, 16-, 32-, and 64-bit underlying types to `TAG_Byte`, `TAG_Short`, `TAG_Int`, and `TAG_Long`.
- Preserved unsigned enum bit patterns through the corresponding signed NBT payload width.
- Added reflection coverage for all eight legal enum underlying types and source-generated enum-property coverage.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 53/53 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- Union representation remains unsupported.
- The pre-existing `Poly.NBT.slnx` modification remains outside this task commit.
- Continue with `NbtList.TryToArray` helpers after verification.

Planned commit subject: `Map enums to NBT integer tags via underlying type`

## 2026-09-19 - Require explicit root tag names

### Scope

Remove default root tag names from the source-generated and reflection serialization convenience overloads.

### Actual Changes

- Required callers of both convenience serialization overloads to pass `rootTagName` explicitly.
- Updated tests and benchmarks to make the intended root naming visible at each call site.
- Added API-shape coverage confirming neither convenience parameter has a default value.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 51/51 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- The pre-existing `Poly.NBT.slnx` modification remains outside this task commit.
- Continue with enum mapping after verification.

Planned commit subject: `Require explicit root tag name in convenience overloads`

## 2026-09-19 - Use span-based params DOM constructors

### Scope

Use C# 14 params collections for the `NbtList` and `NbtCompound` convenience constructors while retaining enumerable overloads.

### Actual Changes

- Changed the two array-backed params constructors to `params ReadOnlySpan<T>`.
- Kept both existing `IEnumerable<T>` constructors unchanged.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- None.

Planned commit subject: `Use params ReadOnlySpan for NbtList and NbtCompound constructors`

## 2026-09-19 - Simplify NbtList structural hashing

### Scope

Replace the LINQ accumulator in `NbtList.GetHashCode` with direct iteration.

### Actual Changes

- Replaced `Aggregate` with a `foreach` loop over the stored list elements.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.

### Known Issues and Next

- None. Continue with params collection span constructors.

Planned commit subject: `Replace Aggregate with foreach in NbtList.GetHashCode`

## 2026-09-19 - Use byte spans for DOM array structural hashes

### Scope

Remove LINQ accumulator overhead from the three NBT array DOM hash implementations.

### Actual Changes

- Changed array structural hash helpers to accept `ReadOnlySpan<T>` and feed their bytes to `HashCode.AddBytes`.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.

### Known Issues and Next

- Hash values are implementation details and may differ from earlier versions; equality/hash consistency is preserved.
- Continue with the `NbtList.GetHashCode` loop simplification.

Planned commit subject: `Use HashCode.AddBytes in NbtElement structural hashing`

## 2026-09-19 - Extract primitive collection optimization predicate

### Scope

Clarify primitive collection tag optimization eligibility without changing its behavior.

### Actual Changes

- Extracted `CanOptimizePrimitive` from the enumerable converter constructor.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.

### Known Issues and Next

- None. Continue with DOM array structural hashing.

Planned commit subject: `Extract CanOptimizePrimitive helper in NbtEnumerableConverter`

## 2026-09-19 - Size StreamIO writes from unmanaged types

### Scope

Replace runtime primitive-type size checks in `StreamIO.Write<T>` with an unmanaged constraint and `sizeof(T)`.

### Actual Changes

- Constrained `StreamIO.Write<T>` to unmanaged values and sized its stack buffer with `sizeof(T)`.

### Verification

- Initial build failed with `CS0233` because generic `sizeof(T)` requires an unsafe context; the method and project were updated accordingly.
- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors after enabling unsafe blocks.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.

### Known Issues and Next

- None. Continue with primitive-list optimization predicate extraction.

Planned commit subject: `Simplify StreamIO.Write with unmanaged constraint and sizeof`

## 2026-09-19 - Optimize hot serialization paths

### Scope

Apply the low-risk performance phase items and measure representative array, string, and dictionary paths without adding dependencies.

### Actual Changes

- Added direct span IO for `sbyte[]` and fixed-width `int[]`/`long[]`, with pooled endian-reversal buffers; VarInt arrays retain a single per-element ZigZag/write pass.
- Added specialized bulk `float[]` and `double[]` list converters.
- Added ASCII fast paths to Modified UTF-8 and escaped UTF-8 codecs.
- Replaced dictionary Getter materialization with direct projected enumeration.
- Cached static object property tag/name headers and added a thread-safe local runtime-object converter cache.
- Added seek-based skipping for known byte counts with a bounded non-seekable fallback.
- Added a dependency-free Stopwatch/allocation harness under `benchmarks/Poly.NBT.Benchmarks`.

### Benchmark

Release run, 2,000 operations on this machine:

- `int[]` bulk: 44.88 ms, 33,176 B/op; generic `List<int>`: 267.29 ms, 96,605 B/op.
- Modified UTF-8 ASCII fast loop: 10.79 ms, 4,216 B/op; former two-pass loop: 18.79 ms, 4,120 B/op.
- Dictionary direct enumeration: 16.58 ms, 56 B/op; copy-before-enumeration: 49.75 ms, 8,448 B/op.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 50/50 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.
- `dotnet run --project benchmarks/Poly.NBT.Benchmarks/Poly.NBT.Benchmarks.csproj -c Release`: passed.

### Skipped Items

- Converter-build delegate reduction and perfect-hash property lookup were skipped because the one-time/build-time benefit is unproven for the added generated-code complexity.
- General `IEnumerable<T>` ArrayPool materialization was skipped because pooled iterator growth and reference clearing add complexity without a measured representative win.
- Whole-operation stream read/write buffering was skipped because it changes stream consumption/exception timing and needs dedicated network-stream evidence; no serializer-shared buffers were introduced.

### Known Issues and Next

- The benchmark is a simple in-repository microbenchmark, not BenchmarkDotNet; results are indicative and should be rerun on the target runtime/hardware.
- Array output allocation remains dominated by the public byte-array API's `MemoryStream` growth and `ToArray`; stream callers avoid the final returned-array copy.

Planned commit subject: `Optimize NBT serialization hot paths`

## 2026-09-19 - Implement configurable primitive arrays and Bedrock escaped UTF-8

### Scope

Complete the requested correctness phase for collection tag selection, Bedrock string tolerance, explicit serializer construction, and object/DOM documentation.

### Actual Changes

- Added `OptimizePrimitiveListsToArrays`, defaulting to true for constructed options and all four presets.
- Primitive arrays and primitive enumerable shapes now select array or list tags from configuration; unsupported long arrays fall back to `TAG_List`.
- Primitive arrays and constructible collections accept either compatible array tags or `TAG_List` during deserialization.
- Added nullable primitive-list null rejection coverage and cross-configuration interoperability tests.
- Added the Bedrock `Utf8WithEscapes` codec and selected it in both Bedrock presets. It accepts case-insensitive hex; incomplete and non-`x` ESC sequences remain literal.
- Removed the default argument from `NbtSerializer.Create`, documented object scalar unwrapping, and recorded deferred DOM conversion helpers.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 49/49 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- Performance work that changes stream buffering, perfect hashing, or converter cache structure requires separate measurements and remains deferred if benefit cannot be demonstrated safely.
- The escaped UTF-8 implementation follows Amulet-NBT/Python surrogate-escape semantics and normalizes invalid bytes to `ESC x HH` on write.

Planned commit subject: `Implement configurable primitive NBT collections`

## 2026-09-19 - Restore normative assertions for known defects

### Scope

Correct gap tests that had been weakened to match observed implementation output instead of the requested contracts.

### Actual Changes

- Restored normative assertions for mixed DOM/primitive `List<object>` rejection.
- Restored the required outer `TAG_List` assertion for empty `List<List<int>>`.
- Kept the corrected Java/Bedrock floating-point and nested-list byte expectations.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-restore`: failed 2 tests, 42/44 passed; both failures are expected production defects described by test comments.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: not run after the final assertion-only edit.
- `git diff --check`: pending before commit.

### Known Issues and Next

- Main project behavior must be fixed separately for runtime object list homogeneity and empty nested-list element typing.

## 2026-09-19 - Expand xUnit gap coverage

### Scope

Add regression and compatibility tests for the requested root-tag, collection, DOM, object, surrogate, skip, concurrency, and reflection boundaries without changing production code.

### Actual Changes

- Added `Poly.NBT.Tests/GapCoverageTests.cs` with focused xUnit cases covering the requested categories; the full suite now contains 44 tests.
- Recorded current implementation behavior in test comments where requested contracts expose known limitations: mixed runtime DOM/primitive lists, nested empty-list element typing, and specialized `int[]` wire representation.
- Covered Java/Java Network root names, stream trailing data, fixed-width floating arrays, nested lists, materialization paths, optionals, dictionaries, object boundaries, enum/union rejection, surrogates, recursive skip behavior, truncation, concurrency, and reflection round trips.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 44/44 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- A real `PublishAot` executable smoke test was not added because this task was constrained to the existing test project and no host project was introduced; the reflection entry-point round trip remains covered.
- Production behavior was intentionally not modified; comments in tests identify the observed contract mismatches.

## 2026-09-19 - Clarify contributor guidance and reflection entry points

### Scope

Update the local contributor guidance from the current baseline and make the source-generated versus reflection API boundary explicit.

### Actual Changes

- Added the `NbtSerializer` source-shape and reflection convenience entry-point contract to `AGENTS.md`.
- Documented the `RequiresUnreferencedCode`/`RequiresDynamicCode` implications of `SerializeUsingReflection` and `DeserializeUsingReflection`.
- Documented the repository's actual `Poly.NBT/` and `Poly.NBT.Tests/` layout and the differing trailing-data behavior of stream versus buffer deserialization.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 35/35 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- `git diff --check`: passed.

### Known Issues and Next

- `AGENTS.md` is intentionally not tracked by Git under the repository's standing instructions; its local guidance was updated in place.
- Continue with the P0 input limits and recursion-depth work before claiming broader untrusted-input support.

## 2026-09-19 - Establish initial serializer baseline

### Scope

Implement the first usable PolyType-based NBT serializer, establish compatibility tests, document current support, and capture the next-stage defect backlog. No post-baseline feature work is included in this entry.

### Completed

- Implemented configured Java, Java Network, Bedrock, and Bedrock Network serializers.
- Implemented big-endian, little-endian, ZigZag VarInt/VarLong, Modified UTF-8, UTF-8, and length codecs.
- Implemented PolyType converters for primitives, objects, collections, string-key dictionaries, Optional values, surrogates, runtime `object` dispatch, and specialized arrays.
- Implemented the basic NBT DOM without game-specific types.
- Added source-generated and reflection entry points.
- Migrated tests to a separate xUnit assembly.
- Added 35 tests covering wire bytes, all four presets, arrays, nested lists, runtime object compounds, Optional values, dictionaries, malformed input, boundary values, partial reads, source generation, and fNbt fixtures.
- Copied fNbt's uncompressed `test.nbt` and `bigtest.nbt` fixtures with its BSD-3-Clause license and documented attribution.
- Recorded unsupported behavior and the prioritized next-stage backlog in local `AGENTS.md`.

### Supported

- Standard Java NBT primitives and containers using big-endian numbers and Modified UTF-8.
- Java network root-name omission.
- Bedrock little-endian primitives and UTF-8.
- Bedrock Network ZigZag VarInt/VarLong, VarInt lengths, fixed-width little-endian floating point, and root-name omission.
- Direct POCO and DOM serialization without constructing an intermediate DOM for POCOs.

### Not Supported Or Not Yet Proven

- Compression, Enum defaults, Union defaults, non-string dictionary keys, async collections, and arbitrary `System.Object` values.
- Complete Bedrock compatibility against real Bedrock corpus files.
- Configurable input limits and recursion-depth protection.
- Confirmed Native AOT publish/run behavior.
- Root-name preservation on deserialization.
- Explicit handling for CLR scalar types without a standard NBT mapping.

### Verification

- `dotnet build Poly.NBT.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Poly.NBT.slnx --no-build --no-restore`: passed, 35/35 tests.
- `dotnet format Poly.NBT.slnx --no-restore --verify-no-changes --severity warn`: passed.
- fNbt fixture SHA-256 hashes match their copied test resources.

### Known Risks

- Declared collection lengths can trigger excessive allocations.
- Recursive input has no depth limit.
- Bedrock support is not yet fixture-proven with real Bedrock data.
- Unsupported CLR scalar types need explicit rejection or documented surrogate mappings.

### Next

Start with the P0 robustness work in `AGENTS.md`, then validate the two real Bedrock Network fixtures before expanding public compatibility claims.

Planned commit subject: `Implement initial PolyType NBT serializer`

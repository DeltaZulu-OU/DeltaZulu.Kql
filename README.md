# DeltaZulu.Kql

[![Unit Tests](https://github.com/DeltaZulu-OU/DeltaZulu.Kql/actions/workflows/unit-tests.yml/badge.svg)](https://github.com/DeltaZulu-OU/DeltaZulu.Kql/actions/workflows/unit-tests.yml)

The estate's shared KQL semantic contract, backend-neutral relational IR, and
KQL-to-relational compiler. Wraps `Microsoft.Azure.Kusto.Language` and adds
only what that package does not answer.

```text
KQL source
   │
   ▼
Microsoft.Azure.Kusto.Language
   │
   ▼
DeltaZulu.Kql compiler   (DeltaZulu.Kql.Compilation)
   │
   ▼
RelNode                  (DeltaZulu.Kql.Relational)
   │
   ▼
consumer-specific planner / emitter / runtime
```

This package does not execute queries and does not emit any SQL dialect. It
answers exactly one question — what does this KQL mean, structurally — and
stops. DuckDB SQL, Proton/ClickHouse SQL, query execution, and detection
deployment are consumer concerns (today, DeltaZulu.Platform's).

## Three layers, one package

**The type contract** (`DeltaZulu.Kql`) — which scalar types exist, what
carries them in the CLR, and whether a value can be represented in a declared
type:

```csharp
public static IReadOnlyList<ScalarSymbol> All { get; }
public static ScalarSymbol? FromName(string typeName);
public static Type ClrCarrier(ScalarSymbol type);
public static bool TryNormalize(object? value, ScalarSymbol target,
                                out KqlValue result, out KqlLossReason reason);
```

That surface is deliberately small. Aliases, `IsWiderThan` and the `Is*`
predicates are **not** re-exposed — `ScalarSymbol` already answers them, and a
second way to ask is a second answer waiting to disagree.

**The relational IR** (`DeltaZulu.Kql.Relational`) — `RelNode` and
`ScalarExpr`: a backend-neutral, immutable logical query tree (scan, filter,
project, extend, aggregate, sort, limit, sample, distinct, join, let-binding;
column refs, literals, binary/unary/function-call/case/window scalars). It has
no Kusto.Language syntax types in it anywhere — the Kusto AST is an input to
translation, never part of the output tree — and no SQL, DuckDB, Proton, or
Platform concept in it either. It is meant to be lowered by more than one
consumer: today DeltaZulu.Platform's planner and DuckDB/Proton emitters,
eventually DeltaZulu.LocalStream's physical operator compiler.

**The compiler** (`DeltaZulu.Kql.Compilation`) — the single entry point that
turns KQL source into that tree:

```csharp
public sealed class KqlRelationalCompiler
{
    public KqlCompilationResult Compile(string query, IKqlSchemaCatalog catalog);
}

public sealed record KqlCompilationResult(RelNode? Root, IReadOnlyList<KqlDiagnostic> Diagnostics);
```

`IKqlSchemaCatalog` is the narrowest schema contract the compiler needs — table
existence and column names/types, nothing about approval policy or medallion
layering. A consumer with its own table-approval rules (Platform's
`ApprovedViewCatalog`, gating which views are queryable) implements the
interface as a thin adapter over its own catalog; `KqlSchemaCatalog` is a
small ready-made implementation for a static schema (tests, simple tools).
Unsupported or invalid KQL fails explicitly, with structured diagnostics and
no tree — never a silent fallback or a best-effort partial translation.

## Two rules that decide most questions

**The type is declared, never inspected.** Deriving a KQL type by examining a
CLR value's runtime type is a defect. `KqlValue` carries its declared type
alongside the value so nothing downstream has to guess. `KqlColumnSchema`
carries a column's type as a plain KQL type name for the same reason it is not
a `ScalarSymbol` — see the version span below.

**`TryNormalize` never throws and never rounds.** A value that does not fit its
declared type is rejected *as that field* — a typed null plus a `KqlLossReason`
— leaving the rest of the record intact. This refines reject-not-coerce rather
than reversing it: failing a batch and failing a field both refuse to coerce,
and differ only in blast radius.

## The version span

Rx.Kql 3.5.3 floors the agent process at Kusto.Language **9.2.0**. The platform
runs **12.4.1**. Three majors apart, no single version satisfies both — and
assembly identity only has to agree *within* a process, so this assembly must
span them.

| Boundary | Pin |
|---|---|
| This package | **minimum 9.2.0, never bracket-pinned** |
| Agent process | `[9.2.0]` exact |
| Platform process | `[12.4.1]` exact |
| Parse, LogCluster, Forward | minimum 9.2.0, no exact pin |

Only API verified identical across both versions is used in the type contract:
`ScalarTypes.All`, `ScalarSymbol.IsWiderThan`, `ScalarSymbol.From`,
`ScalarTypes.GetSymbol`. The relational compiler additionally uses the ~30
`Kusto.Language.Syntax` node types, ~55 `SyntaxKind` members, and
`KustoCode.ParseAndAnalyze`/`GlobalState`/`TableSymbol`/`DatabaseSymbol` the
translator needs — verified to compile identically against both versions by a
throwaway probe project before any translator code moved here (see git
history on the extraction commits for the exact method).

Four things inside that surface are **not** identical, and all four are
handled here rather than inherited:

- **`ScalarTypes.All` membership differs** — eleven members in 9.2.0, twelve in
  12.4.1, the extra being `null`. `All` filters by *name*, so the surfaced set is
  the same ten types on both. Referencing `ScalarTypes.Null` as a symbol would not
  even compile against 9.2.0.
- **`single` is an alias of `real` in 9.2.0 and not an alias in 12.4.1.**
  `FromName("single")` returns null on both. Delegating blindly would return
  `real` in one process and null in the other — the same input, two answers,
  silently.
- **`bag_has_key` is not a registered KQL function in 9.2.0** (added later).
  Not a compile-time surface at all — Kusto.Language's own semantic binder
  rejects it at parse time in a 9.2.0-hosted process. Not something this
  package can or should paper over; a query using it is genuinely
  unsupported wherever Kusto.Language 9.2.0 is the runtime.
- **`earliest` is a reserved identifier in 9.2.0 but an ordinary one in
  12.4.1.** Same story: a 9.2.0-hosted process cannot bind a column or
  variable named `earliest`; a 12.4.1-hosted one can.

### `SyntaxKind` is not safe to switch on by value — compare by name

This is the single most important implementation rule in the compiler, and it
is easy to violate by accident: **`Kusto.Language.Syntax.SyntaxKind`'s
underlying integer values are not stable across Kusto.Language versions.**
Verified directly:

| Member | 9.2.0 | 12.4.1 |
|---|---|---|
| `StringLiteralExpression` | 439 | 348 |
| `LongLiteralExpression` | 433 | 342 |
| `EqualExpression` | 487 | 406 |

A C# `switch` on an enum value compiles to an integer comparison against a
constant baked in at compile time. This package compiles against 9.2.0, so a
`switch` on `SyntaxKind` here bakes in 9.2.0's numbers — and a consumer pinned
to a different exact version (Platform, at 12.4.1) loads a `SyntaxKind` whose
`EqualExpression` is a completely different integer, so every case in that
switch would silently fail to match. This is real, not theoretical: it broke
every KQL query containing a string literal or an equality comparison the
first time this compiler was wired into Platform, and no test in *this*
repository caught it, because both test projects here compile their sources
against the *same* Kusto.Language version they run against, so the mismatch
never has a chance to appear.

The fix, and the rule for any code added to this compiler going forward:
compare `SyntaxKind` by name (`someKind.ToString() == "EqualExpression"`, or a
`switch` on `someKind.ToString()`), never by enum value. `.ToString()`
resolves the name from whichever Kusto.Language assembly is actually loaded at
runtime, so it is correct regardless of which version compiled this library.
Reference types (`ScalarSymbol`, `GlobalState`, syntax node types matched via
`is`/type-pattern) do not have this problem — they resolve by member dispatch
against the loaded assembly, which is exactly why the type contract above
never switches on an enum and has never needed this rule until the compiler
was added.

## Testing

The conformance suite is compiled twice from one set of sources and run against
both versions:

| Project | Kusto.Language | How |
|---|---|---|
| `DeltaZulu.Kql.Tests` | `[9.2.0]` | Tests the shipped assembly via project reference |
| `DeltaZulu.Kql.Tests.V1241` | `[12.4.1]` | Recompiles the library sources under 12.4.1 and runs the same suite |

The second is a source build rather than a project reference for a reason: the
library compiles against 9.2.0, so its public signatures carry the assembly
identity `Kusto.Language, Version=9.2.0.0`, which a project referencing it while
overriding the package cannot resolve at compile time. (At *runtime* the mismatch
is fine for reference types — .NET resolves by simple name and a higher version
satisfies a lower reference, which is exactly why the minimum-not-exact pin works
in the platform's process; it is emphatically *not* fine for `SyntaxKind`
comparisons by value, per above.) Compiling the sources is also the stronger
test: it proves the library uses only API that exists in 12.4.1, which a binary
reference cannot tell you.

Both projects glob the library's sources recursively (`**/*.cs`, excluding
`bin`/`obj`) — a source added under a new subdirectory is picked up
automatically by the 12.4.1 conformance build; it does not need a project file
edit to stay covered.

A single hostile-matrix test crosses every value in a deliberately nasty set
against every surfaced type and asserts only that nothing throws and every
produced value matches its declared carrier. It found a real defect on first run
— `Math.Abs(long.MinValue)` throws `OverflowException` — which is the argument for
keeping the fixture hostile rather than representative.

What the conformance suite cannot catch, by construction, is the class of bug
above (`SyntaxKind` compared by value): both test projects compile their
sources against the version they run against, so a value baked in at compile
time always matches at runtime *within either project*. That mismatch only
appears across a real compiled-for-9.2.0 / running-under-12.4.1 boundary,
which means a real consumer pinned to a different exact version than this
package's floor is not just a downstream integration detail — it is part of
this package's own test surface area. Treat a fresh build+test cycle in such a
consumer (Platform, today) as required verification after any change to the
translator, not optional extra diligence.

## Governance

Governed by `DEC-0003` and constrained by `CON-0001` through `CON-0016` in
[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs). The verification
behind the version span is in
`reports/2026-08-15-kusto-language-preflight.md`.

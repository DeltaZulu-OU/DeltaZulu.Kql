# DeltaZulu.Kql

[![Unit Tests](https://github.com/DeltaZulu-OU/DeltaZulu.Kql/actions/workflows/unit-tests.yml/badge.svg)](https://github.com/DeltaZulu-OU/DeltaZulu.Kql/actions/workflows/unit-tests.yml)

The estate's KQL type contract. Wraps `Microsoft.Azure.Kusto.Language` and adds
only what that package does not answer.

## The whole public surface

```csharp
public static IReadOnlyList<ScalarSymbol> All { get; }
public static ScalarSymbol? FromName(string typeName);
public static Type ClrCarrier(ScalarSymbol type);
public static bool TryNormalize(object? value, ScalarSymbol target,
                                out KqlValue result, out KqlLossReason reason);
```

That is deliberately small. Aliases, `IsWiderThan` and the `Is*` predicates are
**not** re-exposed — `ScalarSymbol` already answers them, and a second way to ask
is a second answer waiting to disagree.

## Two rules that decide most questions

**The type is declared, never inspected.** Deriving a KQL type by examining a CLR
value's runtime type is a defect. `KqlValue` carries its declared type alongside
the value so nothing downstream has to guess.

**`TryNormalize` never throws and never rounds.** A value that does not fit its
declared type is rejected *as that field* — a typed null plus a `KqlLossReason` —
leaving the rest of the record intact. This refines reject-not-coerce rather than
reversing it: failing a batch and failing a field both refuse to coerce, and
differ only in blast radius.

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

Only API verified identical across both versions is used: `ScalarTypes.All`,
`ScalarSymbol.IsWiderThan`, `ScalarSymbol.From`, `ScalarTypes.GetSymbol`.

Two things inside that surface are **not** identical, and both are handled here
rather than inherited:

- **`ScalarTypes.All` membership differs** — eleven members in 9.2.0, twelve in
  12.4.1, the extra being `null`. `All` filters by *name*, so the surfaced set is
  the same ten types on both. Referencing `ScalarTypes.Null` as a symbol would not
  even compile against 9.2.0.
- **`single` is an alias of `real` in 9.2.0 and not an alias in 12.4.1.**
  `FromName("single")` returns null on both. Delegating blindly would return
  `real` in one process and null in the other — the same input, two answers,
  silently.

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
is fine — .NET resolves by simple name and a higher version satisfies a lower
reference, which is exactly why the minimum-not-exact pin works in the platform's
process.) Compiling the sources is also the stronger test: it proves the library
uses only API that exists in 12.4.1, which a binary reference cannot tell you.

A single hostile-matrix test crosses every value in a deliberately nasty set
against every surfaced type and asserts only that nothing throws and every
produced value matches its declared carrier. It found a real defect on first run
— `Math.Abs(long.MinValue)` throws `OverflowException` — which is the argument for
keeping the fixture hostile rather than representative.

## Governance

Governed by `DEC-0003` and constrained by `CON-0001` through `CON-0016` in
[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs). The verification
behind the version span is in
`reports/2026-08-15-kusto-language-preflight.md`.

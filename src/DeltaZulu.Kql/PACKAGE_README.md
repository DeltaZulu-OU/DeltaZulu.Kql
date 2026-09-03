# DeltaZulu.Kql

The DeltaZulu KQL semantic contract, relational IR, and compiler. Wraps
`Microsoft.Azure.Kusto.Language` and adds only what that package does not
answer. Does not execute queries and does not emit any SQL dialect.

**Type contract** (`DeltaZulu.Kql`):
- `KqlTypes.All` — the ten surfaced scalar types
- `KqlTypes.FromName` — name and alias resolution
- `KqlTypes.ClrCarrier` — the CLR type carrying each scalar
- `KqlTypes.TryNormalize` — per-field normalisation that never throws, never rounds

The type is **declared, never inspected**. Deriving a KQL type by examining a CLR
value's runtime type is a defect.

**Relational IR** (`DeltaZulu.Kql.Relational`): `RelNode`/`ScalarExpr` — a
backend-neutral, immutable logical query tree.

**Compiler** (`DeltaZulu.Kql.Compilation`): `KqlRelationalCompiler.Compile(kql,
IKqlSchemaCatalog)` turns KQL source into that tree, against a caller-supplied
schema/table contract.

Referenced at a **minimum** of Kusto.Language 9.2.0, never bracket-pinned: this
assembly must bind against 9.2.0 in the agent process and 12.4.1 in the platform
process. See the repository README for why this means every comparison
against a Kusto.Language `SyntaxKind` value in the compiler is by name, never
by enum value.

using Kusto.Language.Symbols;

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// A column's declared KQL type. The type is always <see cref="ScalarSymbol"/> --
/// declared, per the estate's type contract, never inferred from a CLR value.
/// </summary>
public sealed record KqlColumnSchema(string Name, ScalarSymbol Type);

/// <summary>A table's name and column schema, as the KQL compiler needs to bind against it.</summary>
public sealed record KqlTableSchema(string Name, IReadOnlyList<KqlColumnSchema> Columns);

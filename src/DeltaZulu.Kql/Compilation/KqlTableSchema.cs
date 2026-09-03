namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// A column's declared KQL type, identified by its canonical KQL type name
/// (e.g. "string", "long", "datetime" -- one of the names in
/// <see cref="DeltaZulu.Kql.KqlTypes.All"/>). The type is declared, never
/// inferred from a CLR value.
/// </summary>
/// <remarks>
/// This is a plain name rather than a Kusto.Language <c>ScalarSymbol</c>
/// deliberately. DeltaZulu.Kql compiles against Kusto.Language 9.2.0 as a
/// minimum, so its assembly's public surface carries that assembly identity;
/// a consumer such as Platform that pins a different exact Kusto.Language
/// version (12.4.1) cannot resolve a <c>ScalarSymbol</c>-typed public member
/// across that boundary at compile time -- confirmed by a real CS0012 when a
/// ScalarSymbol-typed <c>KqlColumnSchema.Type</c> was tried against a
/// packed build of this library. A plain name has no such identity and
/// resolves to a <c>ScalarSymbol</c> for a specific Kusto.Language version
/// via <see cref="DeltaZulu.Kql.KqlTypes.FromName"/> only where that version
/// is already fixed (inside this library, or inside a single consumer).
/// </remarks>
public sealed record KqlColumnSchema(string Name, string TypeName);

/// <summary>A table's name and column schema, as the KQL compiler needs to bind against it.</summary>
public sealed record KqlTableSchema(string Name, IReadOnlyList<KqlColumnSchema> Columns);

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// The single public entry point for translating KQL into the relational IR.
/// Parses and binds the query against the given schema catalog, then translates
/// the bound tree into a <see cref="Relational.RelNode"/>. Never executes the
/// query and never selects or targets a SQL backend -- the result is a logical
/// tree for a caller's own planner/emitter to consume.
/// </summary>
public sealed class KqlRelationalCompiler
{
    /// <param name="query">The KQL source text to compile.</param>
    /// <param name="catalog">The schema the query is bound and validated against.</param>
    public KqlCompilationResult Compile(string query, IKqlSchemaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var diagnostics = new KqlDiagnosticBuilder();
        var translator = new KustoQueryTranslator(catalog, diagnostics);
        var root = translator.Translate(query);

        return new KqlCompilationResult(root, diagnostics.Items);
    }
}

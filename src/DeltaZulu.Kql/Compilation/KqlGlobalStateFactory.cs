using Kusto.Language;
using Kusto.Language.Symbols;

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// Builds the Kusto.Language <see cref="GlobalState"/> used to parse and bind KQL
/// against a set of table schemas. This is the only place a
/// <see cref="KqlTableSchema"/> is turned into a Kusto.Language <see cref="TableSymbol"/>.
/// </summary>
public static class KqlGlobalStateFactory
{
    /// <summary>
    /// Builds a fresh <see cref="GlobalState"/> exposing <paramref name="tables"/> as
    /// a single database. Building this is not free -- callers compiling repeatedly
    /// against an unchanged catalog should cache the result rather than call this
    /// on every compilation; see <see cref="IKqlSchemaCatalog.BuildGlobalState"/>.
    /// </summary>
    public static GlobalState Build(IEnumerable<KqlTableSchema> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var symbols = tables.Select(ToTableSymbol).ToArray();
        var database = new DatabaseSymbol("kql", symbols);
        return GlobalState.Default.WithDatabase(database);
    }

    private static TableSymbol ToTableSymbol(KqlTableSchema table)
    {
        var schema = "(" + string.Join(", ", table.Columns.Select(c => $"{c.Name}: {c.TypeName}")) + ")";
        return new TableSymbol(table.Name, schema);
    }
}

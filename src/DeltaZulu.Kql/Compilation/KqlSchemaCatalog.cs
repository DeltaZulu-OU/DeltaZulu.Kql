using Kusto.Language;

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// A minimal, immutable <see cref="IKqlSchemaCatalog"/> over a fixed set of tables,
/// with its <see cref="GlobalState"/> built once and cached for the catalog's
/// lifetime. Useful for tests and for callers with a static schema; a catalog whose
/// tables change over time (Platform's approved views, for example) should
/// implement <see cref="IKqlSchemaCatalog"/> directly so it can invalidate the
/// cache when the table set changes.
/// </summary>
public sealed class KqlSchemaCatalog : IKqlSchemaCatalog
{
    private readonly Dictionary<string, KqlTableSchema> _tables;
    private GlobalState? _cachedGlobalState;

    public KqlSchemaCatalog(IEnumerable<KqlTableSchema> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _tables = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public long Version => 1;

    public IEnumerable<KqlTableSchema> Tables => _tables.Values;

    public bool TryGetTable(string name, out KqlTableSchema schema) => _tables.TryGetValue(name, out schema!);

    public GlobalState BuildGlobalState() => _cachedGlobalState ??= KqlGlobalStateFactory.Build(Tables);
}

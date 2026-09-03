using Kusto.Language;

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// The minimum table-schema surface the KQL relational compiler needs: whether a
/// table exists and its columns' declared KQL types, so the compiler can bind KQL
/// against it. This is deliberately narrower than a full catalog API -- it says
/// nothing about approval policy, medallion layering, or how tables are curated.
/// Those stay with the caller (see the estate's Platform adapter over its own
/// approved-view catalog).
/// </summary>
public interface IKqlSchemaCatalog
{
    /// <summary>
    /// Monotonic version that changes whenever the set of tables changes. Callers
    /// compiling repeatedly against the same catalog can use it as a cache key.
    /// </summary>
    long Version { get; }

    /// <summary>All tables this catalog exposes, in no particular order.</summary>
    IEnumerable<KqlTableSchema> Tables { get; }

    /// <summary>
    /// Resolves <paramref name="name"/> to its schema. KQL table identifiers are
    /// case-insensitive, so implementations must resolve case-insensitively.
    /// </summary>
    bool TryGetTable(string name, out KqlTableSchema schema);

    /// <summary>
    /// Builds the Kusto.Language <see cref="GlobalState"/> used to bind KQL against
    /// this catalog's tables. The default implementation builds a fresh
    /// <see cref="GlobalState"/> on every call via <see cref="KqlGlobalStateFactory"/>;
    /// implementations expecting repeated compilation against an unchanged catalog
    /// should override this to cache the result, keyed on <see cref="Version"/>.
    /// </summary>
    GlobalState BuildGlobalState() => KqlGlobalStateFactory.Build(Tables);
}

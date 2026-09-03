using Kusto.Language.Syntax;

namespace DeltaZulu.Kql.Compilation;

/// <summary>Enforces the unqualified KQL table-reference boundary against the schema catalog.</summary>
internal sealed class KustoTableReferencePolicy
{
    private readonly IKqlSchemaCatalog _catalog;
    private readonly KqlDiagnosticBuilder _diagnostics;

    public KustoTableReferencePolicy(IKqlSchemaCatalog catalog, KqlDiagnosticBuilder diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
    }

    public bool TryValidateTablePathQualifiers(IReadOnlyList<string> parts, out string tableName)
    {
        tableName = parts[^1];
        if (parts.Count == 1) { return true; }
        _diagnostics.AddError(KqlDiagnosticPhase.Policy, BuildQualifiedTablePathRejectedMessage(parts));
        return false;
    }

    public void ValidateQualifiedApprovedTableReferences(SyntaxNode root)
    {
        foreach (var path in root.GetDescendants<PathExpression>())
        {
            var parts = KustoSyntaxHelpers.GetPathParts(path);
            if (parts.Count <= 1 || !_catalog.TryGetTable(parts[^1], out _)) { continue; }
            _diagnostics.AddError(KqlDiagnosticPhase.Policy, BuildQualifiedTablePathRejectedMessage(parts));
        }
    }

    private static string BuildQualifiedTablePathRejectedMessage(IReadOnlyList<string> parts)
        => $"Table path '{string.Join('.', parts)}' is not allowed. Use the unqualified table name '{parts[^1]}'.";
}

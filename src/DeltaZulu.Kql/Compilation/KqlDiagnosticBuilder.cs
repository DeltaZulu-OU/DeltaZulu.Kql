namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// Mutable diagnostic accumulator used internally while compiling. Never exposed
/// publicly -- callers only ever see the immutable <see cref="KqlCompilationResult"/>.
/// </summary>
internal sealed class KqlDiagnosticBuilder
{
    private readonly List<KqlDiagnostic> _items = [];

    public IReadOnlyList<KqlDiagnostic> Items => _items;

    public bool HasErrors => _items.Exists(d => d.IsError);

    public void AddError(KqlDiagnosticPhase phase, string message, string? detail = null, int? start = null, int? length = null, string code = "GEN000")
        => _items.Add(new KqlDiagnostic(KqlDiagnosticSeverity.Error, phase, message, detail, start, length, code));

    public void AddWarning(KqlDiagnosticPhase phase, string message, string? detail = null, string code = "GEN000")
        => _items.Add(new KqlDiagnostic(KqlDiagnosticSeverity.Warning, phase, message, detail, Code: code));
}

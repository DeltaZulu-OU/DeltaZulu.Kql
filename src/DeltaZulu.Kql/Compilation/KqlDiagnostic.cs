namespace DeltaZulu.Kql.Compilation;

/// <summary>Severity of a compilation diagnostic. Errors mean the compilation produced no usable tree.</summary>
public enum KqlDiagnosticSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// Pipeline phase where a diagnostic originated, limited to what KQL-to-relational
/// compilation itself performs. A consumer with further downstream stages (SQL
/// emission, execution) defines its own phases for those rather than extending this one.
/// </summary>
public enum KqlDiagnosticPhase
{
    Parse,
    Policy,
    Translate
}

/// <summary>
/// A single diagnostic produced while compiling KQL to a relational tree.
/// </summary>
public sealed record KqlDiagnostic(
    KqlDiagnosticSeverity Severity,
    KqlDiagnosticPhase Phase,
    string Message,
    string? Detail = null,
    int? TextStart = null,
    int? TextLength = null,
    string Code = "GEN000")
{
    public bool IsError => Severity == KqlDiagnosticSeverity.Error;

    public override string ToString() =>
        Detail is null
            ? $"[{Phase}/{Severity}] {Message}"
            : $"[{Phase}/{Severity}] {Message} | {Detail}";
}

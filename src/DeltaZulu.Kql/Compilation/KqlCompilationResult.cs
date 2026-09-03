using DeltaZulu.Kql.Relational;

namespace DeltaZulu.Kql.Compilation;

/// <summary>
/// The result of compiling KQL to the relational IR: the tree, when compilation
/// succeeded, and every diagnostic produced along the way. A caller never
/// instantiates a mutable diagnostic bag -- the compiler builds this immutably.
/// </summary>
public sealed record KqlCompilationResult(RelNode? Root, IReadOnlyList<KqlDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.IsError);
}

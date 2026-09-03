using DeltaZulu.Kql.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests.Compilation;

[TestClass]
public sealed class KqlDiagnosticTests
{
    [TestMethod]
    public void IsError_TrueOnlyForErrorSeverity()
    {
        var error = new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Translate, "bad");
        var warning = new KqlDiagnostic(KqlDiagnosticSeverity.Warning, KqlDiagnosticPhase.Translate, "hmm");

        Assert.IsTrue(error.IsError);
        Assert.IsFalse(warning.IsError);
    }

    [TestMethod]
    public void Code_DefaultsToUnspecified()
    {
        var diagnostic = new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Parse, "bad");

        Assert.AreEqual("GEN000", diagnostic.Code);
    }

    [TestMethod]
    public void RecordEquality_ComparesAllFields()
    {
        var a = new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Policy, "msg", "detail", 1, 2, "CODE1");
        var b = new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Policy, "msg", "detail", 1, 2, "CODE1");
        var c = a with { Message = "different" };

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }
}

[TestClass]
public sealed class KqlCompilationResultTests
{
    [TestMethod]
    public void HasErrors_FalseWhenNoErrorDiagnostics()
    {
        var result = new KqlCompilationResult(
            Root: null,
            Diagnostics: [new KqlDiagnostic(KqlDiagnosticSeverity.Warning, KqlDiagnosticPhase.Translate, "hmm")]);

        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void HasErrors_TrueWhenAnyErrorDiagnostic()
    {
        var result = new KqlCompilationResult(
            Root: null,
            Diagnostics:
            [
                new KqlDiagnostic(KqlDiagnosticSeverity.Warning, KqlDiagnosticPhase.Translate, "hmm"),
                new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Policy, "no"),
            ]);

        Assert.IsTrue(result.HasErrors);
    }

    [TestMethod]
    public void HasErrors_FalseWhenNoDiagnosticsAtAll()
    {
        var result = new KqlCompilationResult(Root: null, Diagnostics: []);

        Assert.IsFalse(result.HasErrors);
    }
}

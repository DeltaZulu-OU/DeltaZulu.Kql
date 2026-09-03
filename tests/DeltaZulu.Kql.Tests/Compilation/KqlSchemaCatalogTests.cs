using DeltaZulu.Kql.Compilation;
using Kusto.Language.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests.Compilation;

[TestClass]
public sealed class KqlSchemaCatalogTests
{
    private static KqlSchemaCatalog CreateCatalog() => new(
    [
        new KqlTableSchema("ProcessEvent",
        [
            new KqlColumnSchema("Timestamp", ScalarTypes.DateTime),
            new KqlColumnSchema("ProcessId", ScalarTypes.Long),
            new KqlColumnSchema("FileName", ScalarTypes.String),
        ]),
    ]);

    [TestMethod]
    public void TryGetTable_ResolvesRegisteredTable()
    {
        var catalog = CreateCatalog();

        var found = catalog.TryGetTable("ProcessEvent", out var schema);

        Assert.IsTrue(found);
        Assert.AreEqual("ProcessEvent", schema.Name);
        Assert.AreEqual(3, schema.Columns.Count);
    }

    [TestMethod]
    public void TryGetTable_ResolvesCaseInsensitively()
    {
        var catalog = CreateCatalog();

        Assert.IsTrue(catalog.TryGetTable("processevent", out _));
        Assert.IsTrue(catalog.TryGetTable("PROCESSEVENT", out _));
    }

    [TestMethod]
    public void TryGetTable_ReturnsFalseForUnknownTable()
    {
        var catalog = CreateCatalog();

        Assert.IsFalse(catalog.TryGetTable("NoSuchTable", out _));
    }

    [TestMethod]
    public void Tables_EnumeratesEveryRegisteredTable()
    {
        var catalog = CreateCatalog();

        CollectionAssert.AreEquivalent(
            new[] { "ProcessEvent" },
            catalog.Tables.Select(t => t.Name).ToArray());
    }

    [TestMethod]
    public void BuildGlobalState_CachesAcrossCalls()
    {
        var catalog = CreateCatalog();

        var first = catalog.BuildGlobalState();
        var second = catalog.BuildGlobalState();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void BuildGlobalState_RegistersDeclaredTable()
    {
        var catalog = CreateCatalog();

        var globalState = catalog.BuildGlobalState();
        var code = Kusto.Language.KustoCode.ParseAndAnalyze("ProcessEvent | where ProcessId == 1", globalState);

        var errors = code.GetDiagnostics()
            .Where(d => d.Severity == Kusto.Language.DiagnosticSeverity.Error)
            .ToList();

        Assert.IsTrue(errors.Count == 0, "Expected no binding errors, got: " + string.Join("; ", errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void BuildGlobalState_RejectsUnknownTable()
    {
        var catalog = CreateCatalog();

        var globalState = catalog.BuildGlobalState();
        var code = Kusto.Language.KustoCode.ParseAndAnalyze("NoSuchTable | take 1", globalState);

        var errors = code.GetDiagnostics()
            .Where(d => d.Severity == Kusto.Language.DiagnosticSeverity.Error)
            .ToList();

        Assert.IsTrue(errors.Count > 0, "Expected a binding error for an undeclared table.");
    }
}

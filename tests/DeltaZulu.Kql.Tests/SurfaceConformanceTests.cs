using Kusto.Language.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests;

/// <summary>
/// The contract's shape, asserted identically against Kusto.Language 9.2.0 and
/// 12.4.1. These sources are compiled into both test projects; if an assertion
/// here holds on one version and not the other, the version span (CON-0006) has
/// failed and the failure is visible rather than latent.
/// </summary>
[TestClass]
public sealed class SurfaceConformanceTests
{
    private static readonly string[] ExpectedSurface =
    [
        "bool", "datetime", "decimal", "dynamic", "guid",
        "int", "long", "real", "string", "timespan",
    ];

    [TestMethod]
    public void All_IsTheSameTenTypesOnBothVersions()
    {
        var names = KqlTypes.All.Select(s => s.Name).ToArray();

        CollectionAssert.AreEqual(
            ExpectedSurface,
            names,
            "KqlTypes.All must be the same ten types on 9.2.0 and 12.4.1. " +
            "Actual: " + string.Join(", ", names));
    }

    [TestMethod]
    public void All_ExcludesTypeAndNullAndUnknown()
    {
        // `null` is absent from ScalarTypes.All in 9.2.0 and present in 12.4.1
        // (CON-0003); `type` is present in both; `unknown` is in neither. All three
        // must be outside the surfaced set regardless.
        Assert.IsFalse(KqlTypes.All.Any(s => s.Name == "type"));
        Assert.IsFalse(KqlTypes.All.Any(s => s.Name == "null"));
        Assert.IsFalse(KqlTypes.All.Any(s => s.Name == "unknown"));
    }

    [TestMethod]
    public void ClrCarrier_IsTotalOverAll()
    {
        foreach (var type in KqlTypes.All)
        {
            Assert.IsNotNull(
                KqlTypes.ClrCarrier(type),
                $"No CLR carrier for '{type.Name}'.");
        }
    }

    [TestMethod]
    public void ClrCarrier_MapsEachTypeToItsDeclaredCarrier()
    {
        Assert.AreEqual(typeof(bool), KqlTypes.ClrCarrier(ScalarTypes.Bool));
        Assert.AreEqual(typeof(int), KqlTypes.ClrCarrier(ScalarTypes.Int));
        Assert.AreEqual(typeof(long), KqlTypes.ClrCarrier(ScalarTypes.Long));
        Assert.AreEqual(typeof(double), KqlTypes.ClrCarrier(ScalarTypes.Real));
        Assert.AreEqual(typeof(decimal), KqlTypes.ClrCarrier(ScalarTypes.Decimal));
        Assert.AreEqual(typeof(string), KqlTypes.ClrCarrier(ScalarTypes.String));
        Assert.AreEqual(typeof(TimeSpan), KqlTypes.ClrCarrier(ScalarTypes.TimeSpan));
        Assert.AreEqual(typeof(Guid), KqlTypes.ClrCarrier(ScalarTypes.Guid));
        Assert.AreEqual(typeof(object), KqlTypes.ClrCarrier(ScalarTypes.Dynamic));
    }

    [TestMethod]
    public void ClrCarrier_ForDateTime_IsDateTimeAndNeverDateTimeOffset()
    {
        // CON-0001 and CON-0008. This assertion is the tripwire: DateTimeOffset
        // carries an offset KQL cannot express, and Rx.Kql compares it via local
        // wall clock, so two equal instants can compare unequal.
        Assert.AreEqual(typeof(DateTime), KqlTypes.ClrCarrier(ScalarTypes.DateTime));
        Assert.AreNotEqual(typeof(DateTimeOffset), KqlTypes.ClrCarrier(ScalarTypes.DateTime));
    }

    [TestMethod]
    public void ClrCarrier_RejectsATypeOutsideTheSurface()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => KqlTypes.ClrCarrier(ScalarTypes.Type));
    }

    [TestMethod]
    public void WideningAgreesWithIsWiderThan()
    {
        // The estate does not maintain its own widening table; it asks
        // ScalarSymbol. This test asserts that the relation is what CON-0004
        // recorded, on whichever version is under test.
        var expected = new[]
        {
            ("decimal", "int"), ("decimal", "long"), ("decimal", "real"),
            ("long", "int"), ("real", "int"), ("real", "long"),
            ("string", "dynamic"),
        };

        var actual = (from a in KqlTypes.All
                      from b in KqlTypes.All
                      where a.IsWiderThan(b)
                      select (a.Name, b.Name)).ToArray();

        CollectionAssert.AreEquivalent(
            expected,
            actual,
            "Widening lattice differs from CON-0004. Actual: " +
            string.Join(", ", actual.Select(p => $"{p.Item1}>{p.Item2}")));
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests;

/// <summary>
/// Name resolution, asserted identically on both versions. The `single` case is
/// the reason this file exists: it is the one input whose Kusto.Language answer
/// differs across the span.
/// </summary>
[TestClass]
public sealed class FromNameTests
{
    [TestMethod]
    [DataRow("int", "int")]
    [DataRow("int32", "int")]
    [DataRow("uint", "int")]
    [DataRow("uint32", "int")]
    [DataRow("long", "long")]
    [DataRow("int64", "long")]
    [DataRow("ulong", "long")]
    [DataRow("uint64", "long")]
    [DataRow("real", "real")]
    [DataRow("double", "real")]
    [DataRow("float", "real")]
    [DataRow("datetime", "datetime")]
    [DataRow("date", "datetime")]
    [DataRow("timespan", "timespan")]
    [DataRow("time", "timespan")]
    [DataRow("guid", "guid")]
    [DataRow("uniqueid", "guid")]
    [DataRow("uuid", "guid")]
    [DataRow("bool", "bool")]
    [DataRow("boolean", "bool")]
    [DataRow("string", "string")]
    [DataRow("decimal", "decimal")]
    [DataRow("dynamic", "dynamic")]
    public void FromName_ResolvesIdenticallyOnBothVersions(string input, string expected)
        => Assert.AreEqual(expected, KqlTypes.FromName(input)?.Name);

    [TestMethod]
    public void FromName_RejectsSingle_BecauseItIsVersionDivergent()
    {
        // CON-0005: `single` is an alias of `real` in 9.2.0 and not an alias at all
        // in 12.4.1. Delegating would return `real` in the Agent's process and null
        // in Platform's — the same input, two answers, silently. Rejecting on both
        // is the only answer that is the same everywhere.
        Assert.IsNull(KqlTypes.FromName("single"));
    }

    [TestMethod]
    [DataRow("type")]
    [DataRow("null")]
    public void FromName_RejectsNamesOutsideTheSurface(string input)
        => Assert.IsNull(KqlTypes.FromName(input));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("nonsense")]
    [DataRow("i32")]
    [DataRow("u64")]
    public void FromName_ReturnsNullForUnknownNames(string input)
        => Assert.IsNull(KqlTypes.FromName(input));

    [TestMethod]
    public void FromName_ResolvesEveryTypeInAllByItsOwnName()
    {
        foreach (var type in KqlTypes.All)
        {
            Assert.AreEqual(
                type.Name,
                KqlTypes.FromName(type.Name)?.Name,
                $"'{type.Name}' is surfaced but does not resolve by its own name.");
        }
    }
}

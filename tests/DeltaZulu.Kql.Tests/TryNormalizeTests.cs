using Kusto.Language.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests;

/// <summary>
/// Conversion policy at the boundaries. The cases that matter are the ones where a
/// value does <i>not</i> fit: those decide whether the contract rejects or quietly
/// invents a nearby number.
/// </summary>
[TestClass]
public sealed class TryNormalizeTests
{
    [TestMethod]
    public void Null_IsATypedNullAndIsNotALoss()
    {
        var ok = KqlTypes.TryNormalize(null, ScalarTypes.Long, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.IsTrue(v.IsNull);
        Assert.AreEqual(ScalarTypes.Long, v.Type);
        Assert.AreEqual(KqlLossReason.None, why);
    }

    [TestMethod]
    public void SameTypeAndWideningConversionsAreExact()
    {
        AssertAccepted(true, ScalarTypes.Bool, true);
        AssertAccepted(42, ScalarTypes.Int, 42);
        AssertAccepted(42, ScalarTypes.Long, 42L);
        AssertAccepted(42, ScalarTypes.Real, 42d);
        AssertAccepted(42, ScalarTypes.Decimal, 42m);
        AssertAccepted(42L, ScalarTypes.Decimal, 42m);
        AssertAccepted("x", ScalarTypes.String, "x");
    }

    [TestMethod]
    public void LongIntoReal_IsNarrowedOnlyAbove2Pow53()
    {
        AssertAccepted(1L << 53, ScalarTypes.Real, (double)(1L << 53));

        var ok = KqlTypes.TryNormalize((1L << 53) + 1, ScalarTypes.Real, out var v, out var why);

        // The value is still produced. The loss is recorded rather than hidden —
        // a non-zero Narrowed counter is the contract working, not failing.
        Assert.IsTrue(ok);
        Assert.IsFalse(v.IsNull);
        Assert.AreEqual(KqlLossReason.Narrowed, why);
    }

    [TestMethod]
    public void RealIntoDecimal_IsNarrowedWhenFiniteAndInRange()
    {
        var ok = KqlTypes.TryNormalize(1.5d, ScalarTypes.Decimal, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.AreEqual(1.5m, v.Value);
        Assert.AreEqual(KqlLossReason.Narrowed, why);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void RealIntoDecimal_IsUnrepresentableForNaNAndInfinity(double value)
        => AssertRejected(value, ScalarTypes.Decimal, KqlLossReason.Unrepresentable);

    [TestMethod]
    public void RealIntoDecimal_IsOutOfRangeBeyondDecimalsCeiling()
    {
        // CON-0009: System.Decimal tops out near 7.92e28 while KQL decimal carries
        // 38 digits. Rejected rather than saturated — a saturated value is a number
        // that was never in the data.
        AssertRejected(1e30d, ScalarTypes.Decimal, KqlLossReason.OutOfRange);
        AssertRejected(-1e30d, ScalarTypes.Decimal, KqlLossReason.OutOfRange);
    }

    [TestMethod]
    public void UlongAboveLongMaxValue_IsOutOfRange()
    {
        // The alias table maps ulong onto long (CON-0005), so everything above
        // long.MaxValue has no representable type.
        AssertAccepted((ulong)long.MaxValue, ScalarTypes.Long, long.MaxValue);
        AssertRejected((ulong)long.MaxValue + 1, ScalarTypes.Long, KqlLossReason.OutOfRange);
    }

    [TestMethod]
    public void TargetNarrowerThanSource_IsUnrepresentable()
    {
        AssertRejected(1L, ScalarTypes.Int, KqlLossReason.Unrepresentable);
        AssertRejected(1.5d, ScalarTypes.Int, KqlLossReason.Unrepresentable);
        AssertRejected(1.5m, ScalarTypes.Real, KqlLossReason.Unrepresentable);
    }

    [TestMethod]
    public void DynamicIntoString_IsAccepted()
    {
        // CON-0004 has `string > dynamic`, so this is a widening.
        var bag = new Dictionary<string, object> { ["k"] = "v" };
        var ok = KqlTypes.TryNormalize(bag, ScalarTypes.String, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.AreEqual(KqlLossReason.None, why);
        Assert.IsInstanceOfType<string>(v.Value);
    }

    [TestMethod]
    public void ScalarIntoString_IsUnrepresentable()
    {
        // Turning 42 into "42" is exactly the silent coercion DEC-0003 forbids, and
        // it is how a type-loss boundary reappears after being closed.
        AssertRejected(42, ScalarTypes.String, KqlLossReason.Unrepresentable);
        AssertRejected(42L, ScalarTypes.String, KqlLossReason.Unrepresentable);
        AssertRejected(1.5d, ScalarTypes.String, KqlLossReason.Unrepresentable);
        AssertRejected(true, ScalarTypes.String, KqlLossReason.Unrepresentable);
        AssertRejected(Guid.Empty, ScalarTypes.String, KqlLossReason.Unrepresentable);
    }

    [TestMethod]
    public void DateTimeUtc_IsAccepted()
    {
        var utc = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var ok = KqlTypes.TryNormalize(utc, ScalarTypes.DateTime, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.AreEqual(KqlLossReason.None, why);
        Assert.AreEqual(DateTimeKind.Utc, ((DateTime)v.Value!).Kind);
    }

    [TestMethod]
    public void DateTimeLocal_IsConvertedToUtc()
    {
        var local = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Local);
        var ok = KqlTypes.TryNormalize(local, ScalarTypes.DateTime, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.AreEqual(KqlLossReason.None, why);
        var carried = (DateTime)v.Value!;
        Assert.AreEqual(DateTimeKind.Utc, carried.Kind);
        Assert.AreEqual(local.ToUniversalTime(), carried);
    }

    [TestMethod]
    public void DateTimeUnspecified_IsUnrepresentable()
    {
        // An unspecified DateTime names no instant. Assuming UTC invents one;
        // assuming local makes the answer depend on which machine read it.
        var unspecified = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Unspecified);
        AssertRejected(unspecified, ScalarTypes.DateTime, KqlLossReason.Unrepresentable);
    }

    [TestMethod]
    public void DateTimeOffset_IsAcceptedAsItsUtcInstant()
    {
        var dto = new DateTimeOffset(2026, 8, 15, 14, 0, 0, TimeSpan.FromHours(2));
        var ok = KqlTypes.TryNormalize(dto, ScalarTypes.DateTime, out var v, out var why);

        Assert.IsTrue(ok);
        Assert.AreEqual(KqlLossReason.None, why);
        var carried = (DateTime)v.Value!;
        Assert.AreEqual(DateTimeKind.Utc, carried.Kind);
        Assert.AreEqual(dto.UtcDateTime, carried);
        // The carrier is DateTime; the offset does not survive, by design.
        Assert.IsNotInstanceOfType<DateTimeOffset>(v.Value);
    }

    [TestMethod]
    public void TimeSpan_IsCarriedInTicks()
    {
        // CON-0014: ticks are canonical. TimeSpan is already tick-based, so the
        // carrier agrees with the wire and the registry's microseconds default is
        // the outlier.
        var ts = TimeSpan.FromTicks(1234567);
        AssertAccepted(ts, ScalarTypes.TimeSpan, ts);
    }

    [TestMethod]
    public void RejectedValues_AreTypedNullsOfTheTargetType()
    {
        KqlTypes.TryNormalize(1L, ScalarTypes.Int, out var v, out _);

        // The field is rejected; the record is not. The null still knows its type.
        Assert.IsTrue(v.IsNull);
        Assert.AreEqual(ScalarTypes.Int, v.Type);
    }

    [TestMethod]
    public void TryNormalize_NeverThrows_OverAHostileMatrix()
    {
        object?[] values =
        [
            null, true, 0, -1, int.MaxValue, long.MinValue, long.MaxValue,
            ulong.MaxValue, 0d, double.NaN, double.PositiveInfinity, double.Epsilon,
            decimal.MaxValue, decimal.MinValue, "", "x", Guid.Empty,
            TimeSpan.Zero, TimeSpan.MaxValue,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            DateTime.UnixEpoch, DateTimeOffset.UnixEpoch,
            new Dictionary<string, object>(), new object(), Array.Empty<int>(),
        ];

        foreach (var value in values)
        {
            foreach (var target in KqlTypes.All)
            {
                // The contract is that this returns rather than throws, whatever it
                // is handed. A throw here fails a whole batch for one bad field.
                var ok = KqlTypes.TryNormalize(value, target, out var v, out var why);

                if (!ok)
                {
                    Assert.IsTrue(v.IsNull, $"{value ?? "null"} -> {target.Name}");
                    Assert.AreNotEqual(KqlLossReason.None, why);
                }
                else if (!v.IsNull)
                {
                    Assert.IsInstanceOfType(
                        v.Value,
                        KqlTypes.ClrCarrier(target),
                        $"{value} -> {target.Name} produced the wrong carrier");
                }
            }
        }
    }

    private static void AssertAccepted(object input, ScalarSymbol target, object expected)
    {
        var ok = KqlTypes.TryNormalize(input, target, out var v, out var why);

        Assert.IsTrue(ok, $"{input} -> {target.Name} was rejected as {why}");
        Assert.AreEqual(KqlLossReason.None, why);
        Assert.AreEqual(expected, v.Value);
        Assert.AreEqual(target, v.Type);
    }

    private static void AssertRejected(object input, ScalarSymbol target, KqlLossReason expected)
    {
        var ok = KqlTypes.TryNormalize(input, target, out var v, out var why);

        Assert.IsFalse(ok, $"{input} -> {target.Name} was unexpectedly accepted");
        Assert.AreEqual(expected, why);
        Assert.IsTrue(v.IsNull);
        Assert.AreEqual(target, v.Type);
    }
}

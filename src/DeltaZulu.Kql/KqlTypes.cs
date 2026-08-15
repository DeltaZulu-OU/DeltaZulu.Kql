using System.Collections.Frozen;
using System.Globalization;
using Kusto.Language.Symbols;

namespace DeltaZulu.Kql;

/// <summary>
/// The estate's KQL type contract: which scalar types exist, what carries them in
/// the CLR, and whether a given value can be represented in a declared type.
/// </summary>
/// <remarks>
/// <para>
/// This type wraps <c>Microsoft.Azure.Kusto.Language</c> and adds only what that
/// package does not answer. Kusto.Language contains no CLR type mapping (CON-0002),
/// so the carrier map is the estate's to own. Everything
/// <see cref="ScalarSymbol"/> already answers — aliases, <c>IsWiderThan</c>, the
/// <c>Is*</c> predicates — is deliberately <b>not</b> re-exposed here.
/// </para>
/// <para>
/// <b>Version span.</b> This assembly must bind against Kusto.Language 9.2.0 in the
/// Agent's process and 12.4.1 in Platform's (CON-0006), so it uses only the four
/// members verified identical across both. Two things inside that surface are not
/// identical, and both are handled by name rather than by symbol reference:
/// <see cref="ScalarTypes.All"/> membership differs (CON-0003) and the
/// <c>single</c> alias exists only in 9.2.0 (CON-0005).
/// </para>
/// </remarks>
public static class KqlTypes
{
    // Filtering by NAME, not by symbol reference, is load-bearing. ScalarTypes.Null
    // does not exist as a member in 9.2.0, so `s != ScalarTypes.Null` would not
    // compile there; and All has eleven members in 9.2.0 against twelve in 12.4.1.
    // Excluding these two by name yields the identical ten-member set on both.
    private static readonly string[] ExcludedFromSurface = ["type", "null"];

    // `single` resolves to `real` in 9.2.0 and to nothing in 12.4.1. Delegating
    // blindly would make FromName process-dependent for exactly one input, so this
    // assembly takes an explicit position instead: rejected on both. See FromName.
    private const string VersionDivergentAlias = "single";

    private static readonly FrozenDictionary<string, Type> Carriers;

    /// <summary>
    /// The scalar types the estate surfaces: <see cref="ScalarTypes.All"/> without
    /// <c>type</c> and <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Ten members, identical on 9.2.0 and 12.4.1. <c>type</c> describes a type
    /// rather than a value; <c>null</c> is absent entirely from 9.2.0's list; and
    /// <c>unknown</c> is an inference artefact that neither version includes.
    /// </remarks>
    public static IReadOnlyList<ScalarSymbol> All { get; }

    static KqlTypes()
    {
        All = [.. ScalarTypes.All
            .Where(s => !ExcludedFromSurface.Contains(s.Name, StringComparer.Ordinal))
            .OrderBy(s => s.Name, StringComparer.Ordinal)];

        Carriers = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["bool"] = typeof(bool),
            ["int"] = typeof(int),
            ["long"] = typeof(long),
            ["real"] = typeof(double),
            ["decimal"] = typeof(decimal),
            ["string"] = typeof(string),
            // CON-0001: UTC-only, so the carrier is DateTime with Kind=Utc. Never
            // DateTimeOffset — it carries an offset KQL cannot express, and Rx.Kql
            // compares it via local wall clock (CON-0008).
            ["datetime"] = typeof(DateTime),
            // CON-0014: the canonical unit is ticks. TimeSpan is already tick-based,
            // so the carrier and the wire agree and the registry's microseconds
            // default is the outlier.
            ["timespan"] = typeof(TimeSpan),
            ["guid"] = typeof(Guid),
            ["dynamic"] = typeof(object),
        }.ToFrozenDictionary(StringComparer.Ordinal);

        // The carrier map must be total over the surfaced set. Asserting it here
        // turns a missing carrier into a startup failure rather than a per-value
        // surprise on whichever record first happens to use that type.
        var unmapped = All.Where(s => !Carriers.ContainsKey(s.Name))
                          .Select(s => s.Name)
                          .ToArray();
        if (unmapped.Length > 0)
        {
            throw new InvalidOperationException(
                $"ClrCarrier is not total over KqlTypes.All: no carrier for " +
                $"{string.Join(", ", unmapped)}. The Kusto.Language version in use " +
                $"surfaces a scalar this contract does not map.");
        }
    }

    /// <summary>
    /// Resolves a KQL type name or alias to its symbol, or null when the name is
    /// not one of the surfaced types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aliases resolve as Kusto.Language defines them, with two exceptions that
    /// exist to keep the answer identical in both hosts:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>single</c> returns null. It is an alias of <c>real</c> in 9.2.0 and not an
    /// alias in 12.4.1 (CON-0005). Rejecting it in both processes is deterministic;
    /// delegating would silently give two different answers.
    /// </description></item>
    /// <item><description>
    /// <c>type</c> and <c>null</c> return null. They resolve in Kusto.Language but
    /// are not part of the surfaced set.
    /// </description></item>
    /// </list>
    /// <para>
    /// Note that <c>ulong</c> and <c>uint64</c> resolve to <c>long</c>, which is
    /// lossy about signedness: a <c>ulong</c> above <see cref="long.MaxValue"/> has
    /// no representable type. That is a conversion concern, handled by
    /// <see cref="TryNormalize"/> as <see cref="KqlLossReason.OutOfRange"/>.
    /// </para>
    /// </remarks>
    public static ScalarSymbol? FromName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        if (string.Equals(typeName, VersionDivergentAlias, StringComparison.Ordinal))
        {
            return null;
        }

        var symbol = ScalarSymbol.From(typeName);
        if (symbol is null)
        {
            return null;
        }

        return ExcludedFromSurface.Contains(symbol.Name, StringComparer.Ordinal)
            ? null
            : symbol;
    }

    /// <summary>The CLR type that carries values of <paramref name="type"/>.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is not one of the surfaced types.
    /// </exception>
    public static Type ClrCarrier(ScalarSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Carriers.TryGetValue(type.Name, out var carrier)
            ? carrier
            : throw new ArgumentException(
                $"'{type.Name}' is not a surfaced KQL scalar type.", nameof(type));
    }

    /// <summary>
    /// Attempts to represent <paramref name="value"/> in its declared type
    /// <paramref name="target"/>.
    /// </summary>
    /// <param name="value">The value to carry. Null yields a typed null.</param>
    /// <param name="target">The <b>declared</b> target type.</param>
    /// <param name="result">
    /// The normalised value on success, or a typed null of <paramref name="target"/>
    /// on failure. Never a value of some other type.
    /// </param>
    /// <param name="reason">Why representation was lossy or impossible.</param>
    /// <returns>
    /// True when a value was produced — including when
    /// <paramref name="reason"/> is <see cref="KqlLossReason.Narrowed"/>, where the
    /// value exists but precision may have been lost. False when no value could be
    /// produced, in which case <paramref name="result"/> is a typed null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method <b>never throws</b> and <b>never rounds to fit</b>. A value that
    /// does not fit is rejected as this field, leaving the rest of the record intact.
    /// </para>
    /// <para>
    /// That per-field rejection <i>refines</i> reject-not-coerce (DEC-0003); it does
    /// not reverse it. Failing a whole batch and failing a single field both refuse
    /// to coerce — they differ only in blast radius.
    /// </para>
    /// <para>
    /// Examining <paramref name="value"/>'s runtime type here is conversion, not
    /// inference: the target type is supplied by the caller as a declared fact, and
    /// this method only decides whether the value can be carried into it. It never
    /// derives a KQL type from a CLR value.
    /// </para>
    /// </remarks>
    public static bool TryNormalize(
        object? value,
        ScalarSymbol target,
        out KqlValue result,
        out KqlLossReason reason)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!Carriers.ContainsKey(target.Name))
        {
            result = default;
            reason = KqlLossReason.Unrepresentable;
            return false;
        }

        if (value is null)
        {
            result = KqlValue.Null(target);
            reason = KqlLossReason.None;
            return true;
        }

        return target.Name switch
        {
            "bool" => Try(value is bool b ? KqlValue.Bool(b) : null, target, out result, out reason),
            "int" => NormalizeInt(value, target, out result, out reason),
            "long" => NormalizeLong(value, target, out result, out reason),
            "real" => NormalizeReal(value, target, out result, out reason),
            "decimal" => NormalizeDecimal(value, target, out result, out reason),
            "string" => NormalizeString(value, target, out result, out reason),
            "datetime" => NormalizeDateTime(value, target, out result, out reason),
            "timespan" => Try(value is TimeSpan t ? KqlValue.TimeSpan(t) : null, target, out result, out reason),
            "guid" => Try(value is Guid g ? KqlValue.Guid(g) : null, target, out result, out reason),
            "dynamic" => Accept(KqlValue.Dynamic(value), out result, out reason),
            // Not a closed DeltaZulu enum — ScalarSymbol is an open type from
            // Kusto.Language, so a default arm is required rather than banned. It
            // rejects rather than coercing.
            _ => Reject(target, KqlLossReason.Unrepresentable, out result, out reason),
        };
    }

    private static bool Accept(KqlValue value, out KqlValue result, out KqlLossReason reason)
    {
        result = value;
        reason = KqlLossReason.None;
        return true;
    }

    private static bool Reject(
        ScalarSymbol target, KqlLossReason why, out KqlValue result, out KqlLossReason reason)
    {
        result = KqlValue.Null(target);
        reason = why;
        return false;
    }

    private static bool Try(
        KqlValue? candidate, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
        => candidate is { } v
            ? Accept(v, out result, out reason)
            : Reject(target, KqlLossReason.Unrepresentable, out result, out reason);

    private static bool NormalizeInt(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
        => value switch
        {
            int i => Accept(KqlValue.Int(i), out result, out reason),
            // Widening into int from strictly narrower integrals is exact.
            sbyte v => Accept(KqlValue.Int(v), out result, out reason),
            byte v => Accept(KqlValue.Int(v), out result, out reason),
            short v => Accept(KqlValue.Int(v), out result, out reason),
            ushort v => Accept(KqlValue.Int(v), out result, out reason),
            // long, uint, ulong into int would narrow the declared type. Rejected:
            // the target is narrower than the source.
            _ => Reject(target, KqlLossReason.Unrepresentable, out result, out reason),
        };

    private static bool NormalizeLong(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
        => value switch
        {
            long l => Accept(KqlValue.Long(l), out result, out reason),
            int v => Accept(KqlValue.Long(v), out result, out reason),
            uint v => Accept(KqlValue.Long(v), out result, out reason),
            sbyte v => Accept(KqlValue.Long(v), out result, out reason),
            byte v => Accept(KqlValue.Long(v), out result, out reason),
            short v => Accept(KqlValue.Long(v), out result, out reason),
            ushort v => Accept(KqlValue.Long(v), out result, out reason),
            // CON-0005: the alias table maps ulong to long, which is lossy about
            // signedness. Above long.MaxValue there is no representable value.
            ulong u => u <= long.MaxValue
                ? Accept(KqlValue.Long((long)u), out result, out reason)
                : Reject(target, KqlLossReason.OutOfRange, out result, out reason),
            _ => Reject(target, KqlLossReason.Unrepresentable, out result, out reason),
        };

    private static bool NormalizeReal(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
    {
        switch (value)
        {
            case double d:
                return Accept(KqlValue.Real(d), out result, out reason);
            case float f:
                return Accept(KqlValue.Real(f), out result, out reason);
            case int i:
                return Accept(KqlValue.Real(i), out result, out reason);
            case sbyte or byte or short or ushort or uint:
                return Accept(
                    KqlValue.Real(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
                    out result, out reason);
            case long l:
                // Beyond 2^53 a double cannot hold every long exactly. The value is
                // still produced — the loss is recorded, not hidden.
                // Not Math.Abs: Math.Abs(long.MinValue) throws OverflowException,
                // and this method must never throw for any input.
                const long ExactIntegerLimit = 1L << 53;
                var exact = l is >= -ExactIntegerLimit and <= ExactIntegerLimit;
                result = KqlValue.Real(l);
                reason = exact ? KqlLossReason.None : KqlLossReason.Narrowed;
                return true;
            case ulong u:
                var exactU = u <= (1UL << 53);
                result = KqlValue.Real(u);
                reason = exactU ? KqlLossReason.None : KqlLossReason.Narrowed;
                return true;
            default:
                // decimal into real would narrow the declared type (decimal is wider
                // than real per CON-0004).
                return Reject(target, KqlLossReason.Unrepresentable, out result, out reason);
        }
    }

    private static bool NormalizeDecimal(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
    {
        switch (value)
        {
            case decimal m:
                return Accept(KqlValue.Decimal(m), out result, out reason);
            case int or long or sbyte or byte or short or ushort or uint or ulong:
                return Accept(
                    KqlValue.Decimal(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
                    out result, out reason);
            case float or double:
                var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    // System.Decimal has no NaN and no infinities.
                    return Reject(target, KqlLossReason.Unrepresentable, out result, out reason);
                }

                // CON-0009: System.Decimal tops out near 7.92e28, narrower than KQL
                // decimal's 38 digits. Out of range is rejected, never saturated.
                if (d is < (double)decimal.MinValue or > (double)decimal.MaxValue)
                {
                    return Reject(target, KqlLossReason.OutOfRange, out result, out reason);
                }

                // In range but binary floating point does not map exactly onto a
                // decimal carrier, so the conversion is recorded as narrowed.
                result = KqlValue.Decimal((decimal)d);
                reason = KqlLossReason.Narrowed;
                return true;
            default:
                return Reject(target, KqlLossReason.Unrepresentable, out result, out reason);
        }
    }

    private static bool NormalizeString(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
    {
        if (value is string s)
        {
            return Accept(KqlValue.String(s), out result, out reason);
        }

        // CON-0004 has `string > dynamic`, so a dynamic payload widens into string.
        // A value carried by one of the other nine scalars does not: turning an int
        // into "42" is exactly the silent coercion DEC-0003 forbids.
        if (IsScalarCarrier(value))
        {
            return Reject(target, KqlLossReason.Unrepresentable, out result, out reason);
        }

        var rendered = Convert.ToString(value, CultureInfo.InvariantCulture);
        return rendered is null
            ? Reject(target, KqlLossReason.Unrepresentable, out result, out reason)
            : Accept(KqlValue.String(rendered), out result, out reason);
    }

    private static bool IsScalarCarrier(object value) => value
        is bool or sbyte or byte or short or ushort or int or uint or long or ulong
        or float or double or decimal or DateTime or DateTimeOffset or TimeSpan or Guid;

    private static bool NormalizeDateTime(
        object value, ScalarSymbol target, out KqlValue result, out KqlLossReason reason)
        => value switch
        {
            // CON-0001: KQL datetime is UTC-only.
            DateTime { Kind: DateTimeKind.Utc } d
                => Accept(KqlValue.DateTime(d), out result, out reason),
            // Local carries a known instant, so the conversion is exact.
            DateTime { Kind: DateTimeKind.Local } d
                => Accept(KqlValue.DateTime(d.ToUniversalTime()), out result, out reason),
            // Unspecified names no instant. Treating it as UTC would invent one, and
            // treating it as local would make the result depend on where it was read.
            DateTime { Kind: DateTimeKind.Unspecified }
                => Reject(target, KqlLossReason.Unrepresentable, out result, out reason),
            // Accepted on the way in and immediately reduced to UTC. DateTimeOffset
            // is never the carrier (CON-0001, CON-0008).
            DateTimeOffset dto
                => Accept(KqlValue.DateTime(dto.UtcDateTime), out result, out reason),
            _ => Reject(target, KqlLossReason.Unrepresentable, out result, out reason),
        };
}

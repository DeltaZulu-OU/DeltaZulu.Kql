using Kusto.Language.Symbols;

namespace DeltaZulu.Kql;

/// <summary>
/// A value together with the KQL type it was <b>declared</b> to have.
/// </summary>
/// <remarks>
/// <para>
/// The type travels with the value. It is never re-derived by inspecting
/// <see cref="Value"/>'s runtime type — that inversion is the defect this struct
/// exists to prevent.
/// </para>
/// <para>
/// There is no public constructor. Instances come from the per-type factories or
/// from <see cref="KqlTypes.TryNormalize"/>, both of which guarantee that
/// <see cref="Value"/> is either null or an instance of
/// <see cref="KqlTypes.ClrCarrier"/> for <see cref="Type"/>. A public constructor
/// would let a caller pair any value with any type and lose that guarantee.
/// </para>
/// </remarks>
public readonly struct KqlValue : IEquatable<KqlValue>
{
    private KqlValue(ScalarSymbol type, object? value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>The declared KQL type. Never null on a value obtained from a factory.</summary>
    public ScalarSymbol Type { get; }

    /// <summary>
    /// The carried value, or null for a typed null. When non-null, its runtime type
    /// is <see cref="KqlTypes.ClrCarrier"/> for <see cref="Type"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>True when this is a typed null.</summary>
    public bool IsNull => Value is null;

    /// <summary>A typed null of <paramref name="type"/>.</summary>
    /// <remarks>
    /// A typed null is not an absence. It records that the field exists and has a
    /// declared type, but carries no value — which is exactly the distinction that
    /// a bare untyped null destroys.
    /// </remarks>
    public static KqlValue Null(ScalarSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new KqlValue(type, null);
    }

    /// <summary>A KQL <c>bool</c>.</summary>
    public static KqlValue Bool(bool value) => new(ScalarTypes.Bool, value);

    /// <summary>A KQL <c>int</c>.</summary>
    public static KqlValue Int(int value) => new(ScalarTypes.Int, value);

    /// <summary>A KQL <c>long</c>.</summary>
    public static KqlValue Long(long value) => new(ScalarTypes.Long, value);

    /// <summary>A KQL <c>real</c>.</summary>
    public static KqlValue Real(double value) => new(ScalarTypes.Real, value);

    /// <summary>A KQL <c>decimal</c>.</summary>
    public static KqlValue Decimal(decimal value) => new(ScalarTypes.Decimal, value);

    /// <summary>A KQL <c>string</c>.</summary>
    public static KqlValue String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new KqlValue(ScalarTypes.String, value);
    }

    /// <summary>A KQL <c>datetime</c>. The value must already be UTC.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is not <see cref="DateTimeKind.Utc"/>. KQL
    /// <c>datetime</c> is UTC-only (CON-0001); accepting a local or unspecified
    /// instant here would invent a fact about which moment it names.
    /// </exception>
    public static KqlValue DateTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "KQL datetime is UTC-only. Convert with ToUniversalTime() for a local " +
                "value; an unspecified value has no known instant and must be rejected.",
                nameof(value));
        }

        return new KqlValue(ScalarTypes.DateTime, value);
    }

    /// <summary>A KQL <c>timespan</c>. The canonical unit is ticks (CON-0014).</summary>
    public static KqlValue TimeSpan(TimeSpan value) => new(ScalarTypes.TimeSpan, value);

    /// <summary>A KQL <c>guid</c>.</summary>
    public static KqlValue Guid(Guid value) => new(ScalarTypes.Guid, value);

    /// <summary>A KQL <c>dynamic</c>.</summary>
    public static KqlValue Dynamic(object? value) => new(ScalarTypes.Dynamic, value);

    /// <inheritdoc/>
    public bool Equals(KqlValue other) =>
        Equals(Type, other.Type) && Equals(Value, other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KqlValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Type, Value);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(KqlValue left, KqlValue right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(KqlValue left, KqlValue right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() =>
        IsNull ? $"{Type?.Name ?? "?"}(null)" : $"{Type.Name}({Value})";
}

namespace DeltaZulu.Kql;

/// <summary>
/// Why a value could not be represented in its declared KQL type.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>conversion</b>-loss enumeration, owned by <c>DeltaZulu.Kql</c>. It
/// answers "why could this value not be represented".
/// </para>
/// <para>
/// It must never be merged with <c>KqlNullReason</c>, which is a
/// <b>collection</b>-loss enumeration owned by the type-contract catalogue and
/// answers "why is this field absent" (CON-0015). Merging them would make "the
/// process exited" and "the decimal overflowed" the same kind of fact, and no
/// consumer could then tell a collection gap from a representation failure.
/// </para>
/// <para>
/// The enumeration is closed. Do not switch over it with a <c>_ =&gt;</c>
/// fallthrough arm: a fallthrough silently absorbs any member added later.
/// </para>
/// </remarks>
public enum KqlLossReason
{
    /// <summary>No loss. The value is represented exactly in its declared type.</summary>
    None = 0,

    /// <summary>
    /// The value lies outside the range the declared type's CLR carrier can hold.
    /// No value is produced; the field becomes a typed null.
    /// </summary>
    OutOfRange = 1,

    /// <summary>
    /// The value was carried into the declared type, but the carrier cannot hold
    /// every value of the source exactly, so precision may have been lost. A value
    /// <i>is</i> produced — the loss is recorded rather than hidden.
    /// </summary>
    Narrowed = 2,

    /// <summary>
    /// There is no representation of this value in the declared type at all.
    /// No value is produced; the field becomes a typed null.
    /// </summary>
    Unrepresentable = 3,

    /// <summary>
    /// The value's encoding is structurally invalid, so it could not be read at all.
    /// </summary>
    /// <remarks>
    /// <see cref="KqlTypes.TryNormalize"/> never produces this: it receives values
    /// that have already been decoded. It is reserved for callers that decode wire
    /// or text payloads and need to report a structurally broken field using the
    /// same enumeration.
    /// </remarks>
    Malformed = 4,
}

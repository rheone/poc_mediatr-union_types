namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// One immutable audit record: who did what, to what, with which outcome and why. Never carries a
/// token string, a secret, an <c>Authorization</c> header or a request body. Serialized one per line
/// by <see cref="AuditEventJson"/>.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>Gets the unique id of this record.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets when the event was recorded (UTC, from the injectable <see cref="TimeProvider"/>).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the stable dotted action name, such as <c>Impersonation.IssueToken</c> or <c>Product.Create</c>.</summary>
    public required string Action { get; init; }

    /// <summary>Gets the outcome: the runtime case name of a union response (<c>NotAuthorized</c>, <c>ProductDto</c>, ...), or the HTTP status code for a request event.</summary>
    public required string Outcome { get; init; }

    /// <summary>Gets the real caller: the <c>act</c> subject when the principal is impersonated, else the caller's own id.</summary>
    public string? ActorId { get; init; }

    /// <summary>Gets the identity the request ran as (differs from <see cref="ActorId"/> when impersonated).</summary>
    public string? EffectiveId { get; init; }

    /// <summary>Gets a value indicating whether the request was made under an impersonation token.</summary>
    public bool IsImpersonated { get; init; }

    /// <summary>Gets the <c>jti</c> of the impersonation token minted or used, tying every use back to its mint.</summary>
    public string? TokenId { get; init; }

    /// <summary>Gets what the action was aimed at.</summary>
    public string? TargetType { get; init; }

    /// <summary>Gets which one.</summary>
    public string? TargetId { get; init; }

    /// <summary>Gets the recorded reason (impersonation).</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the ticket reference (impersonation).</summary>
    public string? Ticket { get; init; }

    /// <summary>Gets the request's trace id, joining this event to the operational log.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the caller's network address as the server saw it.</summary>
    public string? SourceIp { get; init; }

    /// <summary>Gets extra facts (roles granted, the denial message, the HTTP method and path); empty for none.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>();
}

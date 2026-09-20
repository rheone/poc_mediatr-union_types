namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// What an <see cref="Abstractions.IAuditableRequest{TResponse}"/> says about itself and its outcome:
/// the target, the recorded reason and any extra facts. The behavior adds who, when, the outcome name
/// and the correlation data. Free text is length-limited when the event is built, so a request may
/// pass raw input.
/// </summary>
/// <param name="TargetType">What the action was aimed at (for example <c>Product</c> or <c>User</c>), or <see langword="null"/>.</param>
/// <param name="TargetId">Which one, or <see langword="null"/> when there is none (yet).</param>
/// <param name="Reason">The recorded reason, when the action carries one.</param>
/// <param name="Ticket">The ticket reference, when the action carries one.</param>
/// <param name="Details">Extra facts (roles granted, the denial message); <see langword="null"/> for none. Never a secret.</param>
/// <param name="TokenId">The <c>jti</c> of a token this action minted (a caller's own token id comes from its principal instead); the id only, never the token.</param>
public sealed record AuditDescription(
    string? TargetType = null,
    string? TargetId = null,
    string? Reason = null,
    string? Ticket = null,
    IReadOnlyDictionary<string, string>? Details = null,
    string? TokenId = null
)
{
    /// <summary>Gets a description with nothing to add.</summary>
    public static AuditDescription None { get; } = new();
}

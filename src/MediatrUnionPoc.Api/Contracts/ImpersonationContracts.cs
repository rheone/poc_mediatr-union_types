namespace MediatrUnionPoc.Api.Contracts;

/// <summary>
/// Request body for <see cref="Controllers.ImpersonationController.IssueTokenAsync"/>. Every member is
/// nullable here so a missing one is reported by the application's validator as a per-field 400, like
/// every other rule, rather than by model binding.
/// </summary>
/// <param name="TargetUserId">The identity the token acts as (its <c>sub</c>). Required; not the caller's own id.</param>
/// <param name="Roles">The roles to grant. Each must be assignable, and a non-administrator may grant only roles they hold. Optional.</param>
/// <param name="Reason">Why the impersonation is needed, 10 to 500 characters. Required; recorded in the token and the audit trail.</param>
/// <param name="TicketReference">An optional ticket or case reference, up to 100 characters.</param>
/// <param name="LifetimeMinutes">The requested lifetime; defaults to the configured default and may not exceed the configured maximum.</param>
public sealed record IssueImpersonationTokenRequest(
    string? TargetUserId,
    IReadOnlyList<string>? Roles,
    string? Reason,
    string? TicketReference,
    int? LifetimeMinutes
);

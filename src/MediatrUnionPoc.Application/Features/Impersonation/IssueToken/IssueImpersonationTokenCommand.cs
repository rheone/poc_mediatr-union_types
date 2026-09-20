using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// Mints a short-lived token that acts as another identity. A controlled authentication bypass:
/// gated by the <see cref="AuthorizationPolicies.Impersonator"/> policy before the handler runs, and
/// every rule beyond that (no chaining, assignable roles, no escalation) is enforced by
/// <see cref="IssueImpersonationTokenHandler"/>. Validated by <see cref="IssueImpersonationTokenValidator"/>.
/// </summary>
/// <remarks>
/// The text members are deliberately nullable and not null-guarded: the validator owns those rules, so
/// a missing member is a per-field <c>ValidationErrors</c> outcome rather than a framework 400 or an exception.
/// </remarks>
/// <param name="TargetUserId">The identity to act as (becomes the token's <c>sub</c>).</param>
/// <param name="Roles">The roles to grant; absent or empty grants none.</param>
/// <param name="Reason">Why the impersonation is needed. Required; recorded in the token and the audit trail.</param>
/// <param name="TicketReference">An optional ticket or case reference for the work.</param>
/// <param name="LifetimeMinutes">The requested lifetime; absent means the configured default, and it may not exceed the configured maximum.</param>
/// <param name="Principal">The real caller, checked against <see cref="PolicyName"/> before the handler runs.</param>
/// <exception cref="ArgumentNullException"><paramref name="Principal"/> is <see langword="null"/>.</exception>
public sealed record IssueImpersonationTokenCommand(
    string? TargetUserId,
    IReadOnlyList<string>? Roles,
    string? Reason,
    string? TicketReference,
    int? LifetimeMinutes,
    ClaimsPrincipal Principal
)
    : ICommand<IssueImpersonationTokenResult>,
        IRequiresAuthorization,
        IAuditableRequest<IssueImpersonationTokenResult>
{
    /// <summary>The audit action recorded for every attempt to mint a token.</summary>
    public const string Action = "Impersonation.IssueToken";

    /// <inheritdoc/>
    public string AuditAction => Action;

    /// <inheritdoc/>
    /// <remarks>Fail closed: a token must never reach a caller whose issue was not recorded.</remarks>
    public AuditFailurePolicy AuditFailurePolicy => AuditFailurePolicy.FailClosed;

    /// <inheritdoc/>
    ClaimsPrincipal? IAuditableRequest<IssueImpersonationTokenResult>.AuditPrincipal => Principal;

    /// <inheritdoc/>
    /// <remarks>
    /// Records the target, reason and ticket as requested (trimmed), the roles granted (the token's) or
    /// asked for (a refusal), the id of the token minted, and for a refusal the denial message. Never
    /// the token itself.
    /// </remarks>
    public AuditDescription DescribeAudit(IssueImpersonationTokenResult response)
    {
        var details = new Dictionary<string, string>();
        string? tokenId = null;
        switch (response)
        {
            case ImpersonationToken token:
                details["roles"] = string.Join(",", token.Roles);
                tokenId = token.TokenId;
                break;
            case NotAuthorized denial:
                details["roles"] = RequestedRoles();
                details["denial"] = string.Join(" ", denial.Reasons);
                break;
            case Error error:
                details["roles"] = RequestedRoles();
                details["denial"] = error.Message;
                if (error.Code is not null)
                {
                    details["errorCode"] = error.Code;
                }

                break;
            case ValidationErrors errors:
                details["roles"] = RequestedRoles();
                details["validation"] = errors.ToErrorMessage();
                break;
        }

        return Describe(details, tokenId);
    }

    /// <inheritdoc/>
    public AuditDescription DescribeUnhandledAudit() =>
        Describe(new Dictionary<string, string> { ["roles"] = RequestedRoles() }, null);

    private AuditDescription Describe(Dictionary<string, string> details, string? tokenId) =>
        new(
            "User",
            TargetUserId?.Trim(),
            Reason?.Trim(),
            string.IsNullOrWhiteSpace(TicketReference) ? null : TicketReference.Trim(),
            details,
            tokenId
        );

    private string RequestedRoles() => string.Join(",", (Roles ?? []).Select(role => role?.Trim()));

    /// <summary>The caller's identity, checked against <see cref="PolicyName"/> before the handler runs; never <see langword="null"/>.</summary>
    public ClaimsPrincipal Principal { get; init; } =
        Principal ?? throw new ArgumentNullException(nameof(Principal));

    /// <inheritdoc/>
    public string PolicyName => AuthorizationPolicies.Impersonator;
}

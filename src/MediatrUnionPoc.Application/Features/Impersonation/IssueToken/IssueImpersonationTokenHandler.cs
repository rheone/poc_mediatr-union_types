using System.Security.Claims;
using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// Decides whether a caller who already passed the <see cref="AuthorizationPolicies.Impersonator"/>
/// policy and validation may mint the requested token, and asks the <see cref="IImpersonationTokenIssuer"/>
/// to sign it. The rules, in order, each a <see cref="NotAuthorized"/> outcome rather than an exception:
/// <list type="number">
/// <item><description>impersonation must be enabled (else an <see cref="Error"/>);</description></item>
/// <item><description>the caller must have an id (the token's actor claim must name someone);</description></item>
/// <item><description>the caller must not already be using an impersonation token (no chained or renewed impersonation);</description></item>
/// <item><description>every requested role must be in <see cref="IImpersonationSettings.AssignableRoles"/>;</description></item>
/// <item><description>unless the caller is an <c>Administrator</c>, every requested role must be one the caller holds, so a <c>Support</c> user cannot mint an <c>Administrator</c> token.</description></item>
/// </list>
/// Recording every attempt is not this handler's job: the command opts into the audit stream
/// (<see cref="IssueImpersonationTokenCommand.DescribeAudit"/>), so the
/// <see cref="Common.Behaviors.AuditBehavior{TRequest,TResponse}"/> records the outcome of this handler and
/// of the checks that run before it.
/// </summary>
/// <param name="issuer">Signs the approved token.</param>
/// <param name="settings">Supplies the switch, the default lifetime and the assignable roles.</param>
/// <exception cref="ArgumentNullException"><paramref name="issuer"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
public sealed class IssueImpersonationTokenHandler(
    IImpersonationTokenIssuer issuer,
    IImpersonationSettings settings
) : IRequestHandler<IssueImpersonationTokenCommand, IssueImpersonationTokenResult>
{
    private readonly IImpersonationTokenIssuer _issuer =
        issuer ?? throw new ArgumentNullException(nameof(issuer));

    private readonly IImpersonationSettings _settings =
        settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public Task<IssueImpersonationTokenResult> Handle(
        IssueImpersonationTokenCommand request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(Decide(request));
    }

    private IssueImpersonationTokenResult Decide(IssueImpersonationTokenCommand request)
    {
        // The validator guarantees these are present; trimming makes what is granted and signed
        // the same value the caller's intent was judged on.
        var target = request.TargetUserId!.Trim();
        var reason = request.Reason!.Trim();
        var ticket = string.IsNullOrWhiteSpace(request.TicketReference)
            ? null
            : request.TicketReference.Trim();
        var roles = (request.Roles ?? [])
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var actor = request.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!_settings.Enabled)
        {
            return ImpersonationErrors.Disabled();
        }

        if (string.IsNullOrEmpty(actor))
        {
            return new NotAuthorized([
                "The caller has no subject (sub) claim, so the impersonation could not be attributed to anyone.",
            ]);
        }

        var refusal = Refuse(request.Principal, roles);
        if (refusal is not null)
        {
            return new NotAuthorized([refusal]);
        }

        var lifetime = TimeSpan.FromMinutes(
            request.LifetimeMinutes ?? _settings.DefaultLifetimeMinutes
        );

        return _issuer.Issue(
            new ImpersonationGrant(actor, target, roles, reason, ticket, lifetime)
        );
    }

    private string? Refuse(ClaimsPrincipal caller, List<string> roles)
    {
        if (caller.IsImpersonated())
        {
            return "An impersonation token cannot be used to impersonate: chained or renewed impersonation is not allowed.";
        }

        var notAssignable = roles
            .Where(role => !_settings.AssignableRoles.Contains(role, StringComparer.Ordinal))
            .ToList();
        if (notAssignable.Count > 0)
        {
            return $"Roles that impersonation may not grant: {string.Join(", ", notAssignable)}.";
        }

        if (!caller.IsInRole(AuthorizationRoles.Administrator))
        {
            var notHeld = roles.Where(role => !caller.IsInRole(role)).ToList();
            if (notHeld.Count > 0)
            {
                return $"Only an administrator may grant roles the caller does not hold: {string.Join(", ", notHeld)}.";
            }
        }

        return null;
    }
}

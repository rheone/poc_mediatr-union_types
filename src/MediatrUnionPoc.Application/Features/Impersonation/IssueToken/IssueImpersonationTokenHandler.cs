using System.Security.Claims;
using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.Extensions.Logging;

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
/// Every outcome that reaches this handler is written through <see cref="Audit"/>, the single place a
/// dedicated audit stream will later replace. The token itself is never logged.
/// </summary>
/// <param name="issuer">Signs the approved token.</param>
/// <param name="settings">Supplies the switch, the default lifetime and the assignable roles.</param>
/// <param name="logger">The logger the audit line is written to.</param>
/// <exception cref="ArgumentNullException"><paramref name="issuer"/>, <paramref name="settings"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
public sealed class IssueImpersonationTokenHandler(
    IImpersonationTokenIssuer issuer,
    IImpersonationSettings settings,
    ILogger<IssueImpersonationTokenHandler> logger
) : IRequestHandler<IssueImpersonationTokenCommand, IssueImpersonationTokenResult>
{
    private const string Issued = "Issued";
    private const string Denied = "Denied";
    private const string Disabled = "Disabled";

    private readonly IImpersonationTokenIssuer _issuer =
        issuer ?? throw new ArgumentNullException(nameof(issuer));

    private readonly IImpersonationSettings _settings =
        settings ?? throw new ArgumentNullException(nameof(settings));

    private readonly ILogger<IssueImpersonationTokenHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

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
        // The validator guarantees these are present; trimming makes what is granted, logged and
        // signed the same value the caller's intent was judged on.
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
            Audit(Disabled, actor, target, roles, reason, ticket, "Impersonation is switched off.");
            return ImpersonationErrors.Disabled();
        }

        if (string.IsNullOrEmpty(actor))
        {
            const string noSubject =
                "The caller has no subject (sub) claim, so the impersonation could not be attributed to anyone.";
            Audit(Denied, actor, target, roles, reason, ticket, noSubject);
            return new NotAuthorized([noSubject]);
        }

        var refusal = Refuse(request.Principal, roles);
        if (refusal is not null)
        {
            Audit(Denied, actor, target, roles, reason, ticket, refusal);
            return new NotAuthorized([refusal]);
        }

        var lifetime = TimeSpan.FromMinutes(
            request.LifetimeMinutes ?? _settings.DefaultLifetimeMinutes
        );
        var token = _issuer.Issue(
            new ImpersonationGrant(actor, target, roles, reason, ticket, lifetime)
        );

        Audit(Issued, actor, target, roles, reason, ticket, detail: null);
        return token;
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

    /// <summary>
    /// The single place every attempt that reached this handler is recorded: who, as whom, with which
    /// roles, the outcome and the reason. Structured properties, never the token. A later audit stream
    /// replaces this one method.
    /// </summary>
    private void Audit(
        string outcome,
        string? actor,
        string target,
        List<string> roles,
        string reason,
        string? ticket,
        string? detail
    ) =>
        _logger.Log(
            outcome == Issued ? LogLevel.Information : LogLevel.Warning,
            "Impersonation {Outcome}: actor {ActorId} as {TargetUserId} with roles [{Roles}], reason {Reason}, ticket {Ticket}, detail {Detail}",
            outcome,
            actor,
            target,
            string.Join(", ", roles),
            reason,
            ticket,
            detail
        );
}

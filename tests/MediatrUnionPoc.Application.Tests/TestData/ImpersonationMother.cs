using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>Object mother for the impersonation tests: callers, settings and commands.</summary>
public static class ImpersonationMother
{
    /// <summary>A reason that satisfies the validator.</summary>
    public const string ValidReason = "Reproducing the checkout error alice reported";

    /// <summary>The caller id used for administrators.</summary>
    public const string AdminId = "root";

    /// <summary>The caller id used for support users.</summary>
    public const string SupportId = "sam";

    /// <summary>The usual impersonation target.</summary>
    public const string TargetId = "alice";

    /// <summary>The default lifetime <see cref="Settings"/> reports.</summary>
    public const int DefaultMinutes = 15;

    /// <summary>The maximum lifetime <see cref="Settings"/> reports.</summary>
    public const int MaxMinutes = 60;

    private static readonly string[] DefaultAssignableRoles =
    [
        AuthorizationRoles.Support,
        AuthorizationRoles.Administrator,
    ];

    /// <summary>Builds a caller with an id and roles, optionally already impersonating.</summary>
    /// <param name="id">The <c>NameIdentifier</c>, or <see langword="null"/> for none.</param>
    /// <param name="roles">The roles held.</param>
    /// <param name="marker">Adds the <c>impersonated</c> marker claim.</param>
    /// <param name="actor">Adds an <c>act</c> claim naming this real caller.</param>
    /// <param name="tokenId">Adds a <c>jti</c> claim, as an impersonation token carries.</param>
    /// <returns>An authenticated principal.</returns>
    public static ClaimsPrincipal Caller(
        string? id,
        string[]? roles = null,
        bool marker = false,
        string? actor = null,
        string? tokenId = null
    )
    {
        List<Claim> claims = [];
        if (id is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        }

        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        if (marker)
        {
            claims.Add(new Claim(ImpersonationClaims.Impersonated, "true"));
        }

        if (actor is not null)
        {
            claims.Add(new Claim(ImpersonationClaims.Actor, $$"""{"sub":"{{actor}}"}"""));
        }

        if (tokenId is not null)
        {
            claims.Add(new Claim(ImpersonationClaims.TokenId, tokenId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Builds the settings the tests run under.</summary>
    /// <param name="enabled">The switch.</param>
    /// <param name="assignable">The assignable roles; defaults to Support and Administrator.</param>
    /// <returns>The settings.</returns>
    public static IImpersonationSettings Settings(
        bool enabled = true,
        params string[] assignable
    ) => new StubSettings(enabled, assignable.Length > 0 ? assignable : DefaultAssignableRoles);

    /// <summary>Builds a valid command, overridable per test.</summary>
    /// <param name="principal">The real caller; defaults to an administrator.</param>
    /// <param name="target">The target.</param>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="lifetime">The requested lifetime.</param>
    /// <returns>The command.</returns>
    public static IssueImpersonationTokenCommand Command(
        ClaimsPrincipal? principal = null,
        string? target = TargetId,
        string[]? roles = null,
        string? reason = ValidReason,
        string? ticket = null,
        int? lifetime = null
    ) =>
        new(
            target,
            roles,
            reason,
            ticket,
            lifetime,
            principal ?? Caller(AdminId, [AuthorizationRoles.Administrator])
        );

    private sealed record StubSettings(bool Enabled, IReadOnlyCollection<string> AssignableRoles)
        : IImpersonationSettings
    {
        public int DefaultLifetimeMinutes => DefaultMinutes;

        public int MaxLifetimeMinutes => MaxMinutes;
    }
}

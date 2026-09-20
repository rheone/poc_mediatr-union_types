namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>Names of the roles the authorization handlers and policies check a caller's <see cref="System.Security.Claims.ClaimsPrincipal"/> for.</summary>
public static class AuthorizationRoles
{
    /// <summary>The role that satisfies <see cref="AuthorizationPolicies.Administrator"/>.</summary>
    public const string Administrator = "Administrator";
}

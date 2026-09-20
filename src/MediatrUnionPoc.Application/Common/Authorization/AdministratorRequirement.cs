using Microsoft.AspNetCore.Authorization;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Requires the caller to hold at least one of <see cref="AllowedRoles"/> — the same "any match
/// succeeds, no roles configured means nothing to check" semantics as ASP.NET Core's own built-in
/// <c>RolesAuthorizationRequirement</c>/<c>RolesAuthorizationHandler</c>, kept as a purpose-built
/// type here (rather than reused directly). Two policies use it: <c>Administrator</c> (one role,
/// guarding <c>DeleteProductCommand</c>) and <c>Impersonator</c> (<c>Administrator</c> or
/// <c>Support</c>, guarding the impersonation-token command), both answered by the one
/// <see cref="AdministratorAuthorizationHandler"/>.
/// </summary>
public sealed class AdministratorRequirement : IAuthorizationRequirement
{
    /// <summary>Initializes the requirement with the set of roles that satisfy it.</summary>
    /// <param name="allowedRoles">Any one of these roles is sufficient; an empty set is automatically satisfied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="allowedRoles"/> is <see langword="null"/>.</exception>
    public AdministratorRequirement(params string[] allowedRoles) =>
        AllowedRoles = allowedRoles ?? throw new ArgumentNullException(nameof(allowedRoles));

    /// <summary>The roles that satisfy this requirement — matching any one of them is sufficient.</summary>
    /// <value>A possibly-empty set of role names; an empty set means the requirement is automatically satisfied.</value>
    public IReadOnlyCollection<string> AllowedRoles { get; }
}

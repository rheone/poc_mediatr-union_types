using Microsoft.AspNetCore.Authorization;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Requires the caller to hold at least one of <see cref="AllowedRoles"/> — the same "any match
/// succeeds, no roles configured means nothing to check" semantics as ASP.NET Core's own built-in
/// <c>RolesAuthorizationRequirement</c>/<c>RolesAuthorizationHandler</c>, kept as a purpose-built
/// type here (rather than reused directly) since this POC has exactly one caller: the
/// <c>Administrator</c> policy guarding <c>DeleteProductCommand</c>.
/// </summary>
public sealed class AdministratorRequirement : IAuthorizationRequirement
{
    /// <summary>Initializes the requirement with the set of roles that satisfy it.</summary>
    /// <param name="allowedRoles">Any one of these roles is sufficient; an empty set is automatically satisfied.</param>
    public AdministratorRequirement(params string[] allowedRoles) => AllowedRoles = allowedRoles;

    /// <summary>The roles that satisfy this requirement — matching any one of them is sufficient.</summary>
    public IReadOnlyCollection<string> AllowedRoles { get; }
}

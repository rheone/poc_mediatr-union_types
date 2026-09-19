using Microsoft.AspNetCore.Authorization;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Succeeds an <see cref="AdministratorRequirement"/> when the caller is in at least one of its
/// <see cref="AdministratorRequirement.AllowedRoles"/> — mirrors the OR-across-roles semantics of
/// ASP.NET Core's built-in <c>RolesAuthorizationHandler</c>.
/// </summary>
public sealed class AdministratorAuthorizationHandler
    : AuthorizationHandler<AdministratorRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministratorRequirement requirement
    )
    {
        if (
            requirement.AllowedRoles.Count == 0
            || requirement.AllowedRoles.Any(context.User.IsInRole)
        )
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

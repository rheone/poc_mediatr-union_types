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
    /// <summary>
    /// Guards <paramref name="context"/> before delegating to the framework's evaluation, which would
    /// otherwise fail with a <see cref="NullReferenceException"/> from inside the base class.
    /// </summary>
    /// <param name="context">The authorization context to evaluate.</param>
    /// <returns>A task that completes when evaluation has finished.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override Task HandleAsync(AuthorizationHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return base.HandleAsync(context);
    }

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

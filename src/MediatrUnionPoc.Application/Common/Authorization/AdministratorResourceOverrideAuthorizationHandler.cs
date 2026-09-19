using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Succeeds an <see cref="OperationAuthorizationRequirement"/> for any resource type when the
/// caller holds the <c>Administrator</c> role and the requirement's
/// <see cref="OperationAuthorizationRequirement.Name"/> is one of <see cref="_allowedOperationNames"/>
/// — a role-based bypass layered onto whichever resource-based handler(s) (e.g.
/// <see cref="OwnerAuthorizationHandler{TResource}"/>) are already registered for the same
/// requirement/resource pair. This works without any OR logic in application code because ASP.NET
/// Core's own authorization evaluation succeeds a requirement as soon as any one registered handler
/// succeeds it — see <c>ResourceAuthorizationOrAcrossHandlersTests</c> for that mechanism proven
/// directly against the framework. Scoping to specific operation names (rather than bypassing every
/// resource-based policy this requirement type is ever used for) is deliberate: a caller-supplied
/// <see cref="OperationAuthorizationRequirement.Name"/> like <c>Update</c> can keep requiring
/// ownership while a differently-named one like <c>Delete</c> also accepts this bypass, simply by
/// registering this handler with the operation names it should apply to.
/// </summary>
/// <typeparam name="TResource">
/// The resource type being authorized — unconstrained, since this handler never inspects the
/// resource itself.
/// </typeparam>
/// <param name="allowedOperationNames">
/// The <see cref="OperationAuthorizationRequirement.Name"/> values this bypass applies to; an empty
/// set applies to every operation name, mirroring <see cref="AdministratorRequirement.AllowedRoles"/>'s
/// "empty means unrestricted" convention.
/// </param>
public sealed class AdministratorResourceOverrideAuthorizationHandler<TResource>(
    params string[] allowedOperationNames
) : AuthorizationHandler<OperationAuthorizationRequirement, TResource>
{
    private readonly string[] _allowedOperationNames = allowedOperationNames;

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        TResource resource
    )
    {
        if (
            context.User.IsInRole("Administrator")
            && (
                _allowedOperationNames.Length == 0
                || _allowedOperationNames.Contains(requirement.Name)
            )
        )
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

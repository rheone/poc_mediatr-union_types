using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Succeeds an <see cref="OperationAuthorizationRequirement"/> for any resource type when the
/// caller holds the <c>Administrator</c> role and the requirement's
/// <see cref="OperationAuthorizationRequirement.Name"/> is one of the names passed as
/// <paramref name="allowedOperationNames"/> — a role-based bypass layered onto whichever
/// resource-based handler(s) (e.g.
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
/// <exception cref="ArgumentNullException"><paramref name="allowedOperationNames"/> is <see langword="null"/>.</exception>
public sealed class AdministratorResourceOverrideAuthorizationHandler<TResource>(
    params string[] allowedOperationNames
) : AuthorizationHandler<OperationAuthorizationRequirement, TResource>
{
    private readonly string[] _allowedOperationNames =
        allowedOperationNames ?? throw new ArgumentNullException(nameof(allowedOperationNames));

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

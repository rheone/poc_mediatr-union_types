using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Succeeds an <see cref="OperationAuthorizationRequirement"/> against any resource exposing an
/// <see cref="IOwnedResource.OwnerId"/> when the caller's <see cref="ClaimTypes.NameIdentifier"/>
/// claim matches it. This is the resource-based counterpart to
/// <see cref="AdministratorAuthorizationHandler"/>'s role check: generic over any resource type,
/// registered against the two-generic-parameter <c>AuthorizationHandler&lt;TRequirement,
/// TResource&gt;</c> form (which receives the loaded resource directly in
/// <see cref="HandleRequirementAsync"/>) rather than the one-generic-parameter form used for role
/// checks — per ASP.NET Core's own resource-based authorization model. Because
/// <see cref="OperationAuthorizationRequirement"/> is reused as-is (parameterized by a
/// <see cref="OperationAuthorizationRequirement.Name"/> like <c>Update</c>/<c>Delete</c>), the same
/// handler instance answers every CRUD-shaped operation for <typeparamref name="TResource"/>
/// without a bespoke requirement type per operation.
/// </summary>
/// <typeparam name="TResource">The resource type being authorized, which must expose an owner.</typeparam>
/// <remarks>
/// This handler has no external dependency of its own, so registering it as a singleton (the
/// pattern <see cref="MediatrUnionPoc.Application.DependencyInjection"/> already uses for
/// <see cref="AdministratorAuthorizationHandler"/>) would be safe. A resource handler that instead
/// depends on EF Core — e.g. to re-check an owner against the database rather than trusting the
/// already-loaded resource — must <b>not</b> be registered as a singleton: <c>DbContext</c> and
/// other scoped EF Core services are not safe to share across requests the way a singleton would.
/// Register such a handler scoped (or transient) instead.
/// </remarks>
public sealed class OwnerAuthorizationHandler<TResource>
    : AuthorizationHandler<OperationAuthorizationRequirement, TResource>
    where TResource : IOwnedResource
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
        OperationAuthorizationRequirement requirement,
        TResource resource
    )
    {
        var callerId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (callerId is not null && callerId == resource.OwnerId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

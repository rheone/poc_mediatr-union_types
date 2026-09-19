using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Deletes a product by id. Idempotent in effect but not in result — deleting an already-missing
/// product still returns <c>NotFound</c>, not <c>Success</c>. Only an administrator may delete —
/// see <see cref="Principal"/>.
/// </summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Principal">
/// The caller's identity, checked by <see cref="Common.Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// against the <see cref="PolicyName"/> policy before this command's handler runs.
/// </param>
public sealed record DeleteProductCommand(Guid Id, ClaimsPrincipal Principal)
    : ITransactionalCommand<DeleteProductResult>,
        IRequiresAuthorization
{
    /// <inheritdoc/>
    public string PolicyName => AuthorizationPolicies.Administrator;
}

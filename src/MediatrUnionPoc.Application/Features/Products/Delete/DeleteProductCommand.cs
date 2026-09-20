using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Domain;

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
/// <param name="ExpectedVersion">
/// The <see cref="Domain.ProductVersion"/> the caller last saw (its <c>If-Match</c> ETag), or
/// <see langword="null"/> to delete whatever version is stored. When supplied it is enforced: a
/// mismatch is <see cref="MediatrUnionPoc.Application.Common.Results.PreconditionFailed"/> and
/// nothing is deleted.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="Principal"/> is <see langword="null"/>.</exception>
public sealed record DeleteProductCommand(
    Guid Id,
    ClaimsPrincipal Principal,
    ProductVersion? ExpectedVersion = null
) : ITransactionalCommand<DeleteProductResult>, IRequiresAuthorization
{
    /// <summary>The caller's identity, checked against <see cref="PolicyName"/> before the handler runs; never <see langword="null"/>.</summary>
    public ClaimsPrincipal Principal { get; init; } =
        Principal ?? throw new ArgumentNullException(nameof(Principal));

    /// <inheritdoc/>
    public string PolicyName => AuthorizationPolicies.Administrator;
}

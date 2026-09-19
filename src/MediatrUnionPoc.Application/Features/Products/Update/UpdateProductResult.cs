// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>
/// Everything an "update" command can come back as: no payload on success, a missing entity,
/// invalid input, a caller who doesn't own the product, or an unexpected error. Unlike
/// <see cref="Create.CreateProductResult"/>, this union has no DTO case at all — a successful
/// update returns <see cref="Success"/>, not the updated <see cref="Common.ProductDto"/>.
/// </summary>
/// <remarks>
/// <para>What each case means for this specific operation, as distinct from the generic meaning
/// documented on the case type itself in <c>Common/Results/</c> (e.g. <see cref="Success"/>,
/// <see cref="NotFound{TId}"/>):</para>
/// <list type="bullet">
/// <item><description><see cref="Success"/> — the product existed, the caller owns it, and its
/// name/price were overwritten. <see cref="UpdateProductHandler"/> only ever reaches this after
/// <see cref="Product.UpdateDetails"/> has already been called, so this is also the sole case
/// that must be committed (see <see cref="ShouldCommit"/>).</description></item>
/// <item><description><see cref="NotFound{TId}"/> of <see cref="ProductId"/> — no product exists
/// with the requested id. <c>Id</c> is always populated: this command always targets exactly one
/// id.</description></item>
/// <item><description><see cref="ValidationErrors"/> — <see cref="UpdateProductCommand.Name"/> or
/// <see cref="UpdateProductCommand.Price"/> failed <see cref="UpdateProductValidator"/>'s rules.
/// Produced by <see cref="FromValidationErrors"/> before the product is even loaded.</description></item>
/// <item><description><see cref="Error"/> — reserved for an unexpected/domain failure; nothing in
/// <see cref="UpdateProductHandler"/> currently constructs this case.</description></item>
/// <item><description><see cref="NotAuthorized"/> — the product exists but the caller isn't its
/// owner. Produced by <see cref="UpdateProductHandler"/> itself, after loading the product, via
/// <see cref="MediatrUnionPoc.Application.Common.Authorization.ResourceAuthorizationService"/> — see
/// <see cref="UpdateProductCommand.Principal"/> for why this check can't run earlier in the
/// pipeline.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union UpdateProductResult(Success, NotFound<ProductId>, ValidationErrors, Error, NotAuthorized)
    : IValidatable<UpdateProductResult>,
        ITransactionOutcome<UpdateProductResult>,
        IAuthorizable<UpdateProductResult>
{
    /// <inheritdoc/>
    public static UpdateProductResult FromValidationErrors(ValidationErrors errors) => errors;

    /// <inheritdoc/>
    public static UpdateProductResult FromNotAuthorized(NotAuthorized notAuthorized) =>
        notAuthorized;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>Only <see cref="Success"/> commits; every other case rolls back. This mirrors when
    /// <see cref="UpdateProductHandler"/> calls <see cref="Product.UpdateDetails"/>: all four
    /// rollback cases (<see cref="NotFound{TId}"/>, <see cref="ValidationErrors"/>,
    /// <see cref="Error"/>, <see cref="NotAuthorized"/>) are returned before that call happens, so
    /// there is nothing pending to undo — <see cref="IUnitOfWork.RollbackAsync"/> discards a
    /// transaction with no staged changes in it, rather than compensating for anything. This rule is specific
    /// to this union: exhaustive only over <see cref="UpdateProductResult"/>'s own cases, and not
    /// something later unions or case additions should assume — see
    /// <see cref="Create.CreateProductResult.ShouldCommit"/> for why the exhaustiveness itself
    /// matters.
    /// </para>
    /// </remarks>
    public static bool ShouldCommit(UpdateProductResult response) => response switch
    {
        Success => true,
        NotFound<ProductId> => false,
        ValidationErrors => false,
        Error => false,
        NotAuthorized => false,
    };
}

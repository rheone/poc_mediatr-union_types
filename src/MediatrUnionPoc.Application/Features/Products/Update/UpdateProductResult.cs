// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>
/// Everything an "update" command can come back as: the updated product, a missing entity,
/// invalid input, a caller who doesn't own the product, a stale expected version, or an
/// unexpected error. The updated <see cref="ProductDto"/> is returned (rather than a bare
/// <see cref="Success"/>) so the caller learns the product's new
/// <see cref="ProductDto.Version"/> — the API turns it into the new <c>ETag</c>.
/// </summary>
/// <remarks>
/// <para>What each case means for this specific operation, as distinct from the generic meaning
/// documented on the case type itself in <c>Common/Results/</c> (e.g. <see cref="NotFound{TId}"/>):</para>
/// <list type="bullet">
/// <item><description><see cref="ProductDto"/> — the product existed, the caller owns it, and its
/// name/price were overwritten; the DTO is the product as now stored, at its advanced version. <see cref="UpdateProductHandler"/> only ever reaches this after
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
/// <item><description><see cref="PreconditionFailed"/> — the caller's
/// <see cref="UpdateProductCommand.ExpectedVersion"/> is stale. Produced by
/// <see cref="UpdateProductHandler"/> up front, before mutating anything, or (when a concurrent
/// write slips in between load and commit) by <see cref="FromCommitFailure"/>.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union UpdateProductResult(
    ProductDto,
    NotFound<ProductId>,
    ValidationErrors,
    Error,
    NotAuthorized,
    PreconditionFailed,
    Conflict
)
    : IValidatable<UpdateProductResult>,
        ITransactionOutcome<UpdateProductResult>,
        IAuthorizable<UpdateProductResult>,
        ICommitFailable<UpdateProductResult>
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static UpdateProductResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="notAuthorized"/> is <see langword="null"/>.</exception>
    public static UpdateProductResult FromNotAuthorized(NotAuthorized notAuthorized)
    {
        ArgumentNullException.ThrowIfNull(notAuthorized);

        return notAuthorized;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>Only <see cref="ProductDto"/> commits; every other case rolls back. This mirrors when
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
        ProductDto => true,
        NotFound<ProductId> => false,
        ValidationErrors => false,
        Error => false,
        NotAuthorized => false,
        PreconditionFailed => false,
        Conflict => false,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="ConcurrencyConflict"/> means another writer changed the product between this
    /// request loading it and committing — the caller's version is stale, exactly what the handler's
    /// up-front check reports, so it is a <see cref="PreconditionFailed"/> too. A
    /// <see cref="UniqueViolation"/> means a concurrent request claimed the new name after the
    /// handler's up-front check passed: <see cref="Conflict"/>.
    /// </remarks>
    public static UpdateProductResult FromCommitFailure(CommitFailure failure)
    {
        return failure switch
        {
            ConcurrencyConflict => new PreconditionFailed(
                "The product was changed by another request; reload it and retry."
            ),
            UniqueViolation => ProductConflicts.NameTakenByConcurrentRequest(),
        };
    }
}

using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Patch;

/// <summary>
/// Everything a "patch" command can come back as. The same seven outcomes as
/// <see cref="Update.UpdateProductResult"/>, for the same reasons; only the meaning of the success
/// case differs.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="ProductDto"/> — the product existed, the caller owns it, and the
/// supplied fields were applied; the DTO is the product as now stored, at its advanced version.
/// The sole case that commits.</description></item>
/// <item><description><see cref="NotFound{TId}"/> of <see cref="ProductId"/> — no product with the requested id.</description></item>
/// <item><description><see cref="ValidationErrors"/> — nothing supplied, a supplied field null, or a supplied field out of range; produced by <see cref="FromValidationErrors"/> before the product is loaded.</description></item>
/// <item><description><see cref="Error"/> — reserved for an unexpected failure; nothing constructs it today.</description></item>
/// <item><description><see cref="NotAuthorized"/> — the product exists but the caller is not its owner.</description></item>
/// <item><description><see cref="PreconditionFailed"/> — the caller's <see cref="PatchProductCommand.ExpectedVersion"/> is stale, caught up front by the handler or at commit time by <see cref="FromCommitFailure"/>.</description></item>
/// <item><description><see cref="Conflict"/> — the new name duplicates another product's, caught up front by the handler or at commit time by <see cref="FromCommitFailure"/>.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union PatchProductResult(
    ProductDto,
    NotFound<ProductId>,
    ValidationErrors,
    Error,
    NotAuthorized,
    PreconditionFailed,
    Conflict
)
    : IValidatable<PatchProductResult>,
        ITransactionOutcome<PatchProductResult>,
        IAuthorizable<PatchProductResult>,
        ICommitFailable<PatchProductResult>
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static PatchProductResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="notAuthorized"/> is <see langword="null"/>.</exception>
    public static PatchProductResult FromNotAuthorized(NotAuthorized notAuthorized)
    {
        ArgumentNullException.ThrowIfNull(notAuthorized);

        return notAuthorized;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Only <see cref="ProductDto"/> commits. <see cref="PatchProductHandler"/> returns every other
    /// case before it applies any change, so there is nothing pending for the rollback to undo. The
    /// switch is exhaustive over this union's own cases, so adding a case fails the build here.
    /// </remarks>
    public static bool ShouldCommit(PatchProductResult response) => response switch
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
    /// A <see cref="ConcurrencyConflict"/> means another writer changed the product between load and
    /// commit, so the caller's version is stale: <see cref="PreconditionFailed"/>. A
    /// <see cref="UniqueViolation"/> means a concurrent request claimed the new name after the
    /// handler's up-front check: <see cref="Conflict"/>.
    /// </remarks>
    public static PatchProductResult FromCommitFailure(CommitFailure failure)
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

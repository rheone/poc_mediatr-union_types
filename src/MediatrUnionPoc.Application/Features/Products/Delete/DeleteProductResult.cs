// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Everything a "delete" command can come back as. Deliberately has no <see cref="ValidationErrors"/>
/// case — <see cref="DeleteProductCommand"/> has only one field to validate (a non-empty id), so a
/// failure there is folded into <see cref="Error"/> instead of adding a rarely-used fourth case.
/// </summary>
[DebuggerDisplay("{Value}")]
public union DeleteProductResult(Success, NotFound<ProductId>, Error, NotAuthorized)
    : IValidatable<DeleteProductResult>,
        ITransactionOutcome<DeleteProductResult>,
        IAuthorizable<DeleteProductResult>
{
    /// <inheritdoc/>
    /// <remarks>Maps validation failures onto <see cref="Error"/> since this union has no <see cref="ValidationErrors"/> case of its own.</remarks>
    public static DeleteProductResult FromValidationErrors(ValidationErrors errors) =>
        new Error($"Invalid request: {errors.ToErrorMessage()}", Error.ValidationFailureCode);

    /// <inheritdoc/>
    public static DeleteProductResult FromNotAuthorized(NotAuthorized notAuthorized) =>
        notAuthorized;

    /// <inheritdoc/>
    /// <remarks>Exhaustive over this union's own cases only — see <see cref="Create.CreateProductResult.ShouldCommit"/> for why that matters.</remarks>
    public static bool ShouldCommit(DeleteProductResult response) => response switch
    {
        Success => true,
        NotFound<ProductId> => false,
        Error => false,
        NotAuthorized => false,
    };
}

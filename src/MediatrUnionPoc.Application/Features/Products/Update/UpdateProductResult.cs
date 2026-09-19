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
    /// <remarks>Exhaustive over this union's own cases only — see <see cref="Create.CreateProductResult.ShouldCommit"/> for why that matters.</remarks>
    public static bool ShouldCommit(UpdateProductResult response) => response switch
    {
        Success => true,
        NotFound<ProductId> => false,
        ValidationErrors => false,
        Error => false,
        NotAuthorized => false,
    };
}

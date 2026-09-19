// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetById;

/// <summary>
/// Everything a "get by id" query can come back as. Has no <see cref="ValidationErrors"/> case —
/// <see cref="GetProductByIdQuery"/> validates a single id field, so a failure there is folded
/// into <see cref="Error"/> instead.
/// </summary>
[DebuggerDisplay("{Value}")]
public union GetProductByIdResult(ProductDto, NotFound<ProductId>, Error) : IValidatable<GetProductByIdResult>
{
    /// <inheritdoc/>
    /// <remarks>Maps validation failures onto <see cref="Error"/> since this union has no <see cref="ValidationErrors"/> case of its own.</remarks>
    public static GetProductByIdResult FromValidationErrors(ValidationErrors errors) =>
        new Error($"Invalid request: {errors.ToErrorMessage()}", Error.ValidationFailureCode);
}

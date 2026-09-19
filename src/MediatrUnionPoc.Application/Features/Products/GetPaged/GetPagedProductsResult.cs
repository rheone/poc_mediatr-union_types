// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>
/// Everything a paged listing can come back as. No <c>NotFound</c>
/// case — an out-of-range page simply returns an empty <see cref="PagedResult{T}.Items"/> collection, not a failure.
/// </summary>
[DebuggerDisplay("{Value}")]
public union GetPagedProductsResult(PagedResult<ProductDto>, Error) : IValidatable<GetPagedProductsResult>
{
    /// <inheritdoc/>
    /// <remarks>Maps validation failures onto <see cref="Error"/> since this union has no <see cref="ValidationErrors"/> case of its own.</remarks>
    public static GetPagedProductsResult FromValidationErrors(ValidationErrors errors) =>
        new Error($"Invalid paging request: {errors.ToErrorMessage()}", Error.ValidationFailureCode);
}

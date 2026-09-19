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
/// Everything a paged product listing can come back as. Has no <c>NotFound</c> case — an
/// out-of-range <see cref="GetPagedProductsQuery.PageNumber"/> is not a failure, just a page whose
/// <see cref="PagedResult{T}.Items"/> collection is empty.
/// </summary>
/// <remarks>
/// <para>What each case means for this specific operation, as distinct from the generic meaning
/// documented on the case type itself in <c>Common/Results/</c> (e.g. <see cref="Error"/>):</para>
/// <list type="bullet">
/// <item><description><see cref="PagedResult{T}"/> of <see cref="ProductDto"/> — the requested
/// page, ordered by product name, plus enough metadata (<see cref="PagedResult{T}.PageNumber"/>,
/// <see cref="PagedResult{T}.PageSize"/>, <see cref="PagedResult{T}.TotalCount"/>) for the caller
/// to compute whether further pages exist. This is the only outcome
/// <see cref="GetPagedProductsHandler"/> itself ever produces.</description></item>
/// <item><description><see cref="Error"/> — reached only via <see cref="FromValidationErrors"/>;
/// <see cref="GetPagedProductsHandler"/> never constructs this case directly, since
/// <see cref="GetPagedProductsValidator"/> is the only source of invalid input for this
/// query.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union GetPagedProductsResult(PagedResult<ProductDto>, Error) : IValidatable<GetPagedProductsResult>
{
    /// <inheritdoc/>
    /// <remarks>Maps validation failures onto <see cref="Error"/> since this union has no <see cref="ValidationErrors"/> case of its own.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static GetPagedProductsResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new Error(
            $"Invalid paging request: {errors.ToErrorMessage()}",
            Error.ValidationFailureCode
        );
    }
}

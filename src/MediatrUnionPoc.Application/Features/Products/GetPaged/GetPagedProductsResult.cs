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
/// Everything a filtered, sorted, paged product listing can come back as. Has no <c>NotFound</c>
/// case — an out-of-range <see cref="GetPagedProductsQuery.PageNumber"/> is not a failure, just a
/// page whose <see cref="PagedResult{T}.Items"/> collection is empty.
/// </summary>
/// <remarks>
/// <para>What each case means for this specific operation, as distinct from the generic meaning
/// documented on the case type itself in <c>Common/Results/</c> (e.g. <see cref="Error"/>):</para>
/// <list type="bullet">
/// <item><description><see cref="PagedResult{T}"/> of <see cref="ProductDto"/> — the requested
/// page of matching products in the requested order, plus the applied sort and the paging metadata
/// (total count, total pages, first/last/next/previous page) the caller needs to navigate. This is
/// the only outcome <see cref="GetPagedProductsHandler"/> produces on the success path.</description></item>
/// <item><description><see cref="ValidationErrors"/> — a paging bound, filter or sort was invalid;
/// each failure names the offending field (see <see cref="GetPagedProductsValidator"/>).</description></item>
/// <item><description><see cref="Error"/> — an unexpected failure; nothing in this query's normal
/// operation produces it.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union GetPagedProductsResult(PagedResult<ProductDto>, ValidationErrors, Error)
    : IValidatable<GetPagedProductsResult>
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static GetPagedProductsResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors;
    }
}

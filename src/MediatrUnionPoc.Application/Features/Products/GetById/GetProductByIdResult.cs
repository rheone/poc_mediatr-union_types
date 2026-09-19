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
/// <remarks>
/// <para>What each case means for this specific operation, as distinct from the generic meaning
/// documented on the case type itself in <c>Common/Results/</c> (e.g. <see cref="NotFound{TId}"/>,
/// <see cref="Error"/>):</para>
/// <list type="bullet">
/// <item><description><see cref="ProductDto"/> — the product exists and was loaded successfully;
/// this is the only success outcome <see cref="GetProductByIdHandler"/> ever produces.</description></item>
/// <item><description><see cref="NotFound{TId}"/> of <see cref="ProductId"/> — no product exists
/// with the requested id. Unlike the bulk-operation case the generic type also allows, <c>Id</c> is
/// always populated here: this query always looks up exactly one id.</description></item>
/// <item><description><see cref="Error"/> — reached only via <see cref="FromValidationErrors"/>;
/// <see cref="GetProductByIdHandler"/> itself never constructs this case, since the single field
/// <see cref="GetProductByIdValidator"/> checks (a non-empty id) is the only way this query's input
/// can be invalid.</description></item>
/// </list>
/// </remarks>
[DebuggerDisplay("{Value}")]
public union GetProductByIdResult(ProductDto, NotFound<ProductId>, Error) : IValidatable<GetProductByIdResult>
{
    /// <inheritdoc/>
    /// <remarks>Maps validation failures onto <see cref="Error"/> since this union has no <see cref="ValidationErrors"/> case of its own.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static GetProductByIdResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new Error(
            $"Invalid request: {errors.ToErrorMessage()}",
            Error.ValidationFailureCode
        );
    }
}

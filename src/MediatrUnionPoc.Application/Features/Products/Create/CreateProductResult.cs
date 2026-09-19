// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>
/// Everything a "create" command can come back as: the created resource, invalid input,
/// or an unexpected error. Mix-and-match case types per endpoint — this one has no
/// NotFound because there's nothing to look up before creating.
/// </summary>
[DebuggerDisplay("{Value}")]
public union CreateProductResult(ProductDto, ValidationErrors, Error)
    : IValidatable<CreateProductResult>, ITransactionOutcome<CreateProductResult>
{
    /// <inheritdoc/>
    public static CreateProductResult FromValidationErrors(ValidationErrors errors) => errors;

    /// <inheritdoc/>
    /// <remarks>
    /// This <c>switch</c> is exhaustive over <em>this union's own</em> declared cases only — if
    /// <see cref="CreateProductResult"/> ever gains a fourth case type, this stops compiling until
    /// it's classified here. Nothing outside this file decides what a case means for this command.
    /// </remarks>
    public static bool ShouldCommit(CreateProductResult response) => response switch
    {
        ProductDto => true,
        ValidationErrors => false,
        Error => false,
    };
}

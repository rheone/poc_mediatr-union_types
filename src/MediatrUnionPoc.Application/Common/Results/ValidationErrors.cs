using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>Shared case type: the request failed FluentValidation checks.</summary>
[DebuggerDisplay("{ToErrorMessage(),nq}")]
public sealed record ValidationErrors
{
    /// <summary>Deduplicates <paramref name="errors"/> into <see cref="Errors"/>; <see langword="null"/> is treated as no errors.</summary>
    /// <param name="errors">Every field-level failure that occurred. Accepts any sequence — not just a pre-built collection.</param>
    public ValidationErrors(IEnumerable<ValidationError>? errors) =>
        Errors = errors.ToDistinctReadOnlyCollection();

    /// <summary>Every field-level failure that occurred, deduplicated and read-only.</summary>
    public IReadOnlyCollection<ValidationError> Errors { get; }

    /// <inheritdoc/>
    public bool Equals(ValidationErrors? other) =>
        other is not null && Errors.SetEqual(other.Errors);

    /// <inheritdoc/>
    public override int GetHashCode() => Errors.GetSetHashCode();

    /// <summary>
    /// Joins every <see cref="ValidationError.ErrorMessage"/> with <c>"; "</c>, dropping
    /// <see cref="ValidationError.PropertyName"/>. Used only as a fallback message for unions with
    /// no <see cref="ValidationErrors"/> case of their own — a union that declares the case
    /// (see <see cref="MediatrUnionPoc.Application.Features.Products.Create.CreateProductResult"/>)
    /// surfaces <see cref="ValidationError.PropertyName"/> per field instead (see
    /// <c>ProductsController.ValidationProblemFrom</c>), which this collapses away.
    /// </summary>
    /// <returns>Every <see cref="ValidationError.ErrorMessage"/>, joined with <c>"; "</c>.</returns>
    public string ToErrorMessage() => string.Join("; ", Errors.Select(e => e.ErrorMessage));
}

/// <summary>
/// A single property/message pair produced by FluentValidation.
/// </summary>
/// <param name="PropertyName">
/// The name of the field that failed validation, or <see langword="null"/> when the failure
/// isn't specific to any single property (a model-level rule, for instance).
/// </param>
/// <param name="ErrorMessage">The human-readable failure message.</param>
public sealed record ValidationError(string? PropertyName, string ErrorMessage);

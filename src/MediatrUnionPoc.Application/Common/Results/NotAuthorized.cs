using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>
/// Shared case type: the caller isn't allowed to perform the operation. Produced by
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// for any union implementing <see cref="MediatrUnionPoc.Application.Common.Abstractions.IAuthorizable{TSelf}"/>
/// — see <c>DeleteProductResult</c> for the one union in this POC that does — but part of the same
/// intentionally-shared vocabulary as <see cref="Failure"/>, reusable by any future union without
/// needing a new case type invented for it.
/// </summary>
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed record NotAuthorized
{
    /// <summary>Deduplicates <paramref name="reasons"/> into <see cref="Reasons"/>; <see langword="null"/> is treated as no reasons.</summary>
    /// <param name="reasons">Why the caller isn't authorized. Accepts any sequence — not just a pre-built collection.</param>
    public NotAuthorized(IEnumerable<string>? reasons) =>
        Reasons = reasons.ToDistinctReadOnlyCollection();

    /// <summary>Why the caller isn't authorized, deduplicated and read-only.</summary>
    public IReadOnlyCollection<string> Reasons { get; }

    /// <inheritdoc/>
    public bool Equals(NotAuthorized? other) =>
        other is not null && Reasons.SetEqual(other.Reasons);

    /// <inheritdoc/>
    public override int GetHashCode() => Reasons.GetSetHashCode();

    private string DebuggerDisplayText => string.Join("; ", Reasons);
}

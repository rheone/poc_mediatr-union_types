using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>
/// Shared case type: business-rule failure(s) that are not validation errors. No handler in this
/// POC's Products feature currently produces this case — every failure mode it has needed so far
/// is covered by <see cref="NotFound{TId}"/> or <see cref="Error"/> — but it's part of the shared
/// case type vocabulary this project set out to support, available for a future command whose
/// business rules don't reduce to "not found" or "unexpected error." See
/// <c>FailureAndNotAuthorizedCaseTypeTests</c> for proof it behaves correctly wherever a union
/// does declare it, independent of whether any handler happens to today.
/// </summary>
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed record Failure
{
    /// <summary>Deduplicates <paramref name="reasons"/> into <see cref="Reasons"/>; <see langword="null"/> is treated as no reasons.</summary>
    /// <param name="reasons">Why the business rule check failed. Accepts any sequence — not just a pre-built collection.</param>
    public Failure(IEnumerable<string>? reasons) =>
        Reasons = reasons.ToDistinctReadOnlyCollection();

    /// <summary>Why the business rule check failed, deduplicated and read-only.</summary>
    public IReadOnlyCollection<string> Reasons { get; }

    /// <inheritdoc/>
    public bool Equals(Failure? other) => other is not null && Reasons.SetEqual(other.Reasons);

    /// <inheritdoc/>
    public override int GetHashCode() => Reasons.GetSetHashCode();

    private string DebuggerDisplayText => string.Join("; ", Reasons);
}

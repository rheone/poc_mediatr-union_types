using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>
/// Shared case type: a precondition the caller attached to the request no longer holds — typically
/// that the resource changed since the version the caller last saw (a stale <c>If-Match</c>).
/// Meaning-free like every shared case type: which precondition, and whether the operation was
/// rolled back, is decided by the union that declares it.
/// </summary>
/// <param name="Message">A human-readable description of which precondition failed.</param>
/// <exception cref="ArgumentNullException"><paramref name="Message"/> is <see langword="null"/>.</exception>
[DebuggerDisplay("{Message}")]
public sealed record PreconditionFailed(string Message)
{
    /// <summary>A human-readable description of which precondition failed; never <see langword="null"/>.</summary>
    public string Message { get; init; } =
        Message ?? throw new ArgumentNullException(nameof(Message));
}

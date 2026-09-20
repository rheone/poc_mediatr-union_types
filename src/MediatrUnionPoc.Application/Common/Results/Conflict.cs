using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>
/// Shared case type: the request cannot be carried out because it collides with the current state
/// of the system (for example, a value that must be unique is already taken). Distinct from
/// <see cref="PreconditionFailed"/>, where the caller's own stated expectation was wrong.
/// </summary>
/// <param name="Message">A human-readable description of the collision.</param>
/// <exception cref="ArgumentNullException"><paramref name="Message"/> is <see langword="null"/>.</exception>
[DebuggerDisplay("{Message}")]
public sealed record Conflict(string Message)
{
    /// <summary>A human-readable description of the collision; never <see langword="null"/>.</summary>
    public string Message { get; init; } =
        Message ?? throw new ArgumentNullException(nameof(Message));
}

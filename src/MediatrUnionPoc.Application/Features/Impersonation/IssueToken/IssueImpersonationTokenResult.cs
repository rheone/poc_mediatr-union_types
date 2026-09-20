// Excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// Everything an "issue impersonation token" command can come back as: the token
/// (<see cref="ImpersonationToken"/>), invalid input (<see cref="ValidationErrors"/>), a refusal
/// (<see cref="NotAuthorized"/>: the caller is not allowed to impersonate this way, or is already
/// impersonating), or the feature being unavailable (<see cref="Error"/>, code
/// <see cref="ImpersonationErrors.DisabledCode"/>). The command is not transactional, so this union
/// declares no commit classification.
/// </summary>
[DebuggerDisplay("{Value}")]
public union IssueImpersonationTokenResult(
    ImpersonationToken,
    ValidationErrors,
    NotAuthorized,
    Error
) : IValidatable<IssueImpersonationTokenResult>, IAuthorizable<IssueImpersonationTokenResult>
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static IssueImpersonationTokenResult FromValidationErrors(ValidationErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="notAuthorized"/> is <see langword="null"/>.</exception>
    public static IssueImpersonationTokenResult FromNotAuthorized(NotAuthorized notAuthorized)
    {
        ArgumentNullException.ThrowIfNull(notAuthorized);

        return notAuthorized;
    }
}

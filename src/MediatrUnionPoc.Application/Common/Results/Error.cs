using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>Shared case type: an unexpected/domain error with a stable code for API consumers.</summary>
/// <param name="Message">A human-readable description of what went wrong.</param>
/// <param name="Code">A stable, machine-readable code identifying the error, when the failure has one to report.</param>
/// <param name="Cause">
/// The exception that produced this error, if any — a diagnostics-only slot for logging/debugging.
/// Deliberately excluded from serialization (<see cref="JsonIgnoreAttribute"/>): it's never meant
/// to reach an API consumer, only whoever is looking at this <see cref="Error"/> in a debugger or a log.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="Message"/> is <see langword="null"/>.</exception>
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed record Error(
    string Message,
    string? Code = null,
    [property: JsonIgnore] Exception? Cause = null
)
{
    /// <summary>A human-readable description of what went wrong; never <see langword="null"/>.</summary>
    public string Message { get; init; } =
        Message ?? throw new ArgumentNullException(nameof(Message));

    /// <summary>
    /// The <see cref="Code"/> an <see cref="Error"/> carries when a union with no
    /// <see cref="ValidationErrors"/> case of its own folds a validation failure into <see cref="Error"/>
    /// instead (see
    /// <see cref="MediatrUnionPoc.Application.Features.Products.Delete.DeleteProductResult.FromValidationErrors(ValidationErrors)"/>
    /// and similar). This is a fact about <see cref="Error"/>'s own vocabulary of stable codes — not a
    /// property of <see cref="ValidationErrors"/>, which never carries or needs a code itself.
    /// </summary>
    public const string ValidationFailureCode = "VALIDATION_ERROR";

    private string DebuggerDisplayText => Code is null ? Message : $"{Code}: {Message}";
}

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>Case type: an unexpected/domain error with a stable code for API consumers.</summary>
/// <param name="Message">A human-readable description of what went wrong.</param>
/// <param name="Code">A stable, machine-readable code identifying the error, when the failure has one to report.</param>
/// <param name="Cause">
/// The exception that produced this error, if any — a diagnostics-only slot for logging/debugging.
/// Deliberately excluded from serialization (<see cref="JsonIgnoreAttribute"/>): it's never meant
/// to reach an API consumer, only whoever is looking at this <see cref="Error"/> in a debugger or a log.
/// </param>
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed record Error(
    string Message,
    string? Code = null,
    [property: JsonIgnore] Exception? Cause = null
)
{
    /// <summary>
    /// The <see cref="Code"/> an <see cref="Error"/> carries when a union with no
    /// <see cref="ValidationErrors"/> case of its own folds a validation failure into <see cref="Error"/>
    /// instead (see <c>DeleteProductResult.FromValidationErrors</c> and similar). This is a fact
    /// about <see cref="Error"/>'s own vocabulary of stable codes — not a property of
    /// <see cref="ValidationErrors"/>, which never carries or needs a code itself.
    /// </summary>
    public const string ValidationFailureCode = "VALIDATION_ERROR";

    private string DebuggerDisplayText => Code is null ? Message : $"{Code}: {Message}";
}

using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>The <see cref="Error"/> vocabulary of the impersonation feature, defined once so the handler and the host agree on the codes.</summary>
public static class ImpersonationErrors
{
    /// <summary>The <see cref="Error.Code"/> reported when impersonation is switched off.</summary>
    public const string DisabledCode = "IMPERSONATION_DISABLED";

    /// <summary>Builds the <see cref="Error"/> reported when impersonation is switched off; the host maps its code to 404.</summary>
    /// <returns>The error case.</returns>
    public static Error Disabled() => new("Impersonation is not available.", DisabledCode);
}

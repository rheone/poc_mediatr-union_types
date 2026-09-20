namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>
/// The names of the named request-timeout policies. Every endpoint is covered by the default policy
/// (see <see cref="RequestTimeoutOptions.Default"/>) unless it names one of these with
/// <c>[RequestTimeout(RequestTimeoutPolicyNames.Impersonation)]</c> or opts out with
/// <c>[DisableRequestTimeout]</c> in plain sight.
/// </summary>
public static class RequestTimeoutPolicyNames
{
    /// <summary>The shorter policy for minting impersonation tokens.</summary>
    public const string Impersonation = "Impersonation";
}

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>
/// The names of the rate-limiting policies, by intent. An action opts into one with
/// <c>[EnableRateLimiting(RateLimitPolicyNames.Writes)]</c>; an action that names none is limited by
/// <see cref="Reads"/> (see <see cref="RateLimitingServiceCollectionExtensions.WithDefaultRateLimiting"/>),
/// and an endpoint is unlimited only when it says <c>DisableRateLimiting</c> so in plain sight.
/// </summary>
public static class RateLimitPolicyNames
{
    /// <summary>The policy for reads (<c>GET</c>) and, by default, for any action that declares none.</summary>
    public const string Reads = "Reads";

    /// <summary>The policy for mutating product requests (<c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c>).</summary>
    public const string Writes = "Writes";

    /// <summary>The much tighter policy for minting impersonation tokens.</summary>
    public const string Impersonation = "Impersonation";
}

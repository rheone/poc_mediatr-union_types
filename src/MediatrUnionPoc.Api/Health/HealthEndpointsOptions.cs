using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.Health;

/// <summary>
/// Settings for the health endpoints registered by
/// <see cref="HealthServiceCollectionExtensions.AddHealthEndpoints"/>. Bound from the
/// <see cref="SectionName"/> configuration section and validated on start (by the source-generated
/// <see cref="HealthEndpointsOptionsValidator"/>), so a malformed path stops the host from starting
/// instead of surfacing as a routing surprise later.
/// </summary>
public sealed class HealthEndpointsOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "HealthEndpoints";

    /// <summary>Gets or sets the path of the liveness endpoint, which runs no checks. Must start with a slash. Defaults to <c>/health/live</c>.</summary>
    [Required]
    [RegularExpression("^/.*", ErrorMessage = "The liveness path must start with '/'.")]
    public string LivePath { get; set; } = "/health/live";

    /// <summary>Gets or sets the path of the readiness endpoint, which runs the checks tagged <c>ready</c>. Must start with a slash. Defaults to <c>/health/ready</c>.</summary>
    [Required]
    [RegularExpression("^/.*", ErrorMessage = "The readiness path must start with '/'.")]
    public string ReadyPath { get; set; } = "/health/ready";
}

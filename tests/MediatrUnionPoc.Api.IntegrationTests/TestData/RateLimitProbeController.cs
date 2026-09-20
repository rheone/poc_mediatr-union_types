using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>
/// A throwaway controller, added to a host as an application part by the secure-by-default tests only. It
/// stands in for "an action someone adds later": <see cref="Unannotated"/> says nothing about rate
/// limiting and must be limited anyway; the other two declare their intent and must be honoured.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("probe")]
public sealed class RateLimitProbeController : ControllerBase
{
    /// <summary>An action with no rate-limiting attribute at all.</summary>
    /// <returns>200.</returns>
    [HttpGet("unannotated")]
    public IActionResult Unannotated() => Ok();

    /// <summary>An action that opts out of rate limiting.</summary>
    /// <returns>200.</returns>
    [HttpGet("exempt")]
    [DisableRateLimiting]
    public IActionResult Exempt() => Ok();

    /// <summary>A read that names the <c>Writes</c> policy, to prove a declared policy beats the default.</summary>
    /// <returns>200.</returns>
    [HttpGet("writes")]
    [EnableRateLimiting("Writes")]
    public IActionResult Writes() => Ok();
}

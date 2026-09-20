using Asp.Versioning;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>
/// A throwaway controller, added to a host as an application part by the request-timeout tests only. It
/// stands in for "an action someone adds later": <see cref="UnannotatedAsync"/> says nothing about timeouts and
/// must be covered anyway; the others declare their intent and must be honoured. Every action waits on the
/// request's cancellation token, which is what a well-behaved handler does.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("timeout-probe")]
public sealed class RequestTimeoutProbeController : ControllerBase
{
    /// <summary>The time the exempt action works for: far longer than any timeout the tests configure.</summary>
    public static readonly TimeSpan ExemptWork = TimeSpan.FromMilliseconds(600);

    /// <summary>An action with no timeout attribute at all, that never finishes on its own.</summary>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>Never, unless cancelled.</returns>
    [HttpGet("unannotated")]
    public async Task<IActionResult> UnannotatedAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return NoContent();
    }

    /// <summary>An action that opts out of timeouts and works for <see cref="ExemptWork"/>.</summary>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>200 once the work is done.</returns>
    [HttpGet("exempt")]
    [DisableRequestTimeout]
    public async Task<IActionResult> ExemptAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(ExemptWork, cancellationToken);
        return Ok();
    }

    /// <summary>An action that names the <c>Impersonation</c> policy and never finishes on its own, to prove a declared policy beats the default.</summary>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>Never, unless cancelled.</returns>
    [HttpGet("named")]
    [RequestTimeout("Impersonation")]
    public async Task<IActionResult> NamedAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return Ok();
    }
}

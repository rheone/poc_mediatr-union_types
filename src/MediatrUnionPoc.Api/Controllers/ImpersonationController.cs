using Asp.Versioning;
using MediatR;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.RateLimiting;
using MediatrUnionPoc.Api.RequestTimeouts;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediatrUnionPoc.Api.Controllers;

/// <summary>
/// Impersonation: minting a short-lived token that acts as another identity. A controlled
/// authentication bypass, so every response carries <c>Cache-Control: no-store</c>. Case-to-status mapping
/// (also declared via <c>ProducesResponseType</c>):
/// <list type="table">
/// <listheader><term>Action</term><description>Cases</description></listheader>
/// <item><term>IssueTokenAsync</term><description>ImpersonationToken 200; ValidationErrors 400; NotAuthorized 403 (caller is not an Administrator or Support user, is already impersonating, or asked for a role it may not grant); impersonation switched off 404 (for every authenticated caller); Error 500; over the much tighter <c>Impersonation</c> rate limit (per real caller) 429, which is also audited; not finished within the shorter <c>Impersonation</c> request timeout 504 (the token is never delivered).</description></item>
/// </list>
/// </summary>
/// <param name="sender">The MediatR sender the action dispatches its request through.</param>
/// <param name="settings">Supplies the impersonation on/off switch.</param>
/// <exception cref="ArgumentNullException"><paramref name="sender"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiVersions.VersionedPrefix + "/impersonation")]
[Route(ApiVersions.UnversionedAliasPrefix + "/impersonation", Order = 1)] // transitional alias for v1
public sealed class ImpersonationController(ISender sender, IImpersonationSettings settings)
    : ControllerBase
{
    private readonly ISender _sender = sender ?? throw new ArgumentNullException(nameof(sender));

    private readonly IImpersonationSettings _settings =
        settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// Mints a short-lived bearer token that acts as <see cref="IssueImpersonationTokenRequest.TargetUserId"/>.
    /// Only an <c>Administrator</c> or <c>Support</c> caller may ask; see
    /// <see cref="IssueImpersonationTokenHandler"/> for the rules on chaining and on which roles may be granted.
    /// The token's <c>act</c> claim names the real caller and its reason is recorded.
    /// </summary>
    /// <param name="request">Who to act as, which roles to grant, why, and for how long.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 200 with the <see cref="ImpersonationToken"/> (the token string, its expiry and the effective
    /// identity); 400 with per-field errors; 403 if the caller may not do this; 404 if impersonation is
    /// switched off; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPost("tokens")]
    [EnableRateLimiting(RateLimitPolicyNames.Impersonation)]
    [RequestTimeout(RequestTimeoutPolicyNames.Impersonation)]
    [ProducesResponseType(typeof(ImpersonationToken), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IssueTokenAsync(
        IssueImpersonationTokenRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // The token is a credential: no intermediary or browser may cache the response, success or not.
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        // Switched off means "does not exist" for everyone, so this runs before the pipeline can
        // answer an unqualified caller with a 403 that confirms the endpoint is there. (Authentication
        // has already run: an anonymous caller still gets the 401.) The handler refuses with the same
        // error should a request ever reach it another way, and the switch below maps it to 404 too.
        if (!_settings.Enabled)
        {
            return ImpersonationErrors.Disabled().ToProblemResult(HttpContext);
        }

        var result = await _sender.Send(
            new IssueImpersonationTokenCommand(
                request.TargetUserId,
                request.Roles,
                request.Reason,
                request.TicketReference,
                request.LifetimeMinutes,
                User
            ),
            cancellationToken
        );

        return result switch
        {
            ImpersonationToken token => Ok(token),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
            NotAuthorized notAuthorized => notAuthorized.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
        };
    }
}

using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Renders the authentication/authorization <em>middleware's</em> 401 (no or invalid credentials)
/// and 403 (authenticated but refused) as <c>application/problem+json</c> carrying the
/// <c>traceId</c> member, like every other non-2xx response. The framework's own handling still runs
/// first (it sets the status and, for a bearer challenge, the <c>WWW-Authenticate</c> header); this
/// only adds the body. Because a content type is then set, <c>UseStatusCodePages</c> sees a response
/// that already has a body and leaves it alone, so nothing is written twice. The union-driven
/// <c>NotAuthorized</c> 403 a controller produces after the middleware has passed is a separate,
/// unaffected path.
/// </summary>
/// <param name="problemDetails">Writes the problem body (and stamps the trace id through the shared customisation).</param>
/// <param name="httpMapping">Supplies the RFC 7807 <c>type</c> URI for the status.</param>
/// <exception cref="ArgumentNullException"><paramref name="problemDetails"/> or <paramref name="httpMapping"/> is <see langword="null"/>.</exception>
public sealed class ProblemDetailsAuthorizationResultHandler(
    IProblemDetailsService problemDetails,
    IOptions<HttpMappingOptions> httpMapping
) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    private readonly IProblemDetailsService _problemDetails =
        problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));

    private readonly HttpMappingOptions _httpMapping = (
        httpMapping ?? throw new ArgumentNullException(nameof(httpMapping))
    ).Value;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="next"/>, <paramref name="context"/>, <paramref name="policy"/> or <paramref name="authorizeResult"/> is <see langword="null"/>.</exception>
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult
    )
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        await _default.HandleAsync(next, context, policy, authorizeResult);

        if (authorizeResult.Succeeded || context.Response.HasStarted)
        {
            return;
        }

        var (status, title, detail) = authorizeResult.Challenged
            ? (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required: send a valid bearer token in the Authorization header."
            )
            : (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to perform this action."
            );

        context.Response.StatusCode = status;
        await _problemDetails.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = status,
                    Title = title,
                    Detail = detail,
                    Type = _httpMapping.TypeUriFor(status),
                },
            }
        );
    }
}

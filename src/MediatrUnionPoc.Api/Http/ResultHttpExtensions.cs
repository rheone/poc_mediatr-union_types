using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// C# 14 extension members that turn the shared union case types into RFC 7807 responses, so a
/// controller's exhaustive <c>switch</c> keeps one short arm per case instead of repeating the
/// same <c>Problem(...)</c> boilerplate. Each member reads shared policy from
/// <see cref="HttpMappingOptions"/> (via <see cref="HttpContext.RequestServices"/>) and accepts
/// optional per-call overrides; nothing here is required — a controller can write its own arm, or
/// its own extension members, for any case.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>The problem-details extension member carrying a stable machine-readable code.</summary>
    public const string CodeExtensionName = "code";

    /// <summary>The <see cref="CodeExtensionName"/> value on a not-found problem.</summary>
    public const string NotFoundCode = "NOT_FOUND";

    private const string ProblemJson = "application/problem+json";

    extension(Error error)
    {
        /// <summary>
        /// Builds the problem response for this error. The status comes from
        /// <see cref="HttpMappingOptions.StatusCodeFor"/> unless overridden; the title defaults to
        /// the error's code and the detail to its message.
        /// </summary>
        /// <param name="http">The current request context, used to reach <see cref="HttpMappingOptions"/> and the problem-details factory.</param>
        /// <param name="statusCode">Overrides the mapped status for this call.</param>
        /// <param name="title">Overrides the title (default: the error's code).</param>
        /// <param name="detail">Overrides the detail (default: the error's message).</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>application/problem+json</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
        public IActionResult ToProblemResult(
            HttpContext http,
            int? statusCode = null,
            string? title = null,
            string? detail = null
        )
        {
            ArgumentNullException.ThrowIfNull(http);

            return BuildProblem(
                http,
                statusCode ?? OptionsOf(http).StatusCodeFor(error),
                title ?? error.Code,
                detail ?? error.Message
            );
        }
    }

    extension<TId>(NotFound<TId> notFound)
        where TId : struct
    {
        /// <summary>Builds a 404 problem response carrying the <see cref="NotFoundCode"/> extension member.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="resource">The resource's display name used in the default detail (<c>"{resource} '{id}' was not found."</c>).</param>
        /// <param name="statusCode">Overrides the 404 status for this call.</param>
        /// <param name="title">Overrides the title (default: <c>"Not Found"</c>).</param>
        /// <param name="detail">Overrides the whole detail text.</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>application/problem+json</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
        public IActionResult ToProblemResult(
            HttpContext http,
            string resource = "Resource",
            int? statusCode = null,
            string? title = null,
            string? detail = null
        )
        {
            ArgumentNullException.ThrowIfNull(http);

            return BuildProblem(
                http,
                statusCode ?? StatusCodes.Status404NotFound,
                title ?? "Not Found",
                detail ?? $"{resource} '{notFound.Id}' was not found.",
                (CodeExtensionName, NotFoundCode)
            );
        }
    }

    extension(NotAuthorized notAuthorized)
    {
        /// <summary>Builds a 403 problem response whose detail joins the denial reasons with <c>"; "</c>.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="statusCode">Overrides the 403 status for this call.</param>
        /// <param name="title">Overrides the title (default: <c>"Forbidden"</c>).</param>
        /// <param name="detail">Overrides the detail (default: the joined reasons).</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>application/problem+json</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
        public IActionResult ToProblemResult(
            HttpContext http,
            int? statusCode = null,
            string? title = null,
            string? detail = null
        )
        {
            ArgumentNullException.ThrowIfNull(http);

            return BuildProblem(
                http,
                statusCode ?? StatusCodes.Status403Forbidden,
                title ?? "Forbidden",
                detail ?? string.Join("; ", notAuthorized.Reasons)
            );
        }
    }

    extension(ValidationErrors errors)
    {
        /// <summary>Builds a validation problem response whose <c>errors</c> member groups messages per property, exactly as MVC's own validation problem does.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="statusCode">Overrides the 400 status for this call.</param>
        /// <param name="title">Overrides the title (default: MVC's "One or more validation errors occurred.").</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>application/problem+json</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
        public IActionResult ToProblemResult(
            HttpContext http,
            int? statusCode = null,
            string? title = null
        )
        {
            ArgumentNullException.ThrowIfNull(http);

            var modelState = new ModelStateDictionary();

            foreach (var error in errors.Errors)
            {
                modelState.AddModelError(error.PropertyName ?? string.Empty, error.ErrorMessage);
            }

            var status = statusCode ?? StatusCodes.Status400BadRequest;
            var problem = http
                .RequestServices.GetRequiredService<ProblemDetailsFactory>()
                .CreateValidationProblemDetails(http, modelState, status, title);
            problem.Type = OptionsOf(http).IncludeTypeUris ? problem.Type : null;

            return AsProblemResult(problem);
        }
    }

    extension(ClaimsPrincipal)
    {
        /// <summary>
        /// Builds the caller's principal from the POC's stand-in identity headers (there is no real
        /// authentication): an <c>Administrator</c> role claim when <paramref name="adminHeader"/>
        /// is <c>"true"</c> (case-insensitive), and a <see cref="ClaimTypes.NameIdentifier"/> claim
        /// when <paramref name="callerIdHeader"/> is non-empty.
        /// </summary>
        /// <param name="adminHeader">The <c>X-Admin</c> header value, or <see langword="null"/> if absent.</param>
        /// <param name="callerIdHeader">The <c>X-Caller-Id</c> header value, or <see langword="null"/> if absent.</param>
        /// <returns>A principal; anonymous (no claims) when neither header is meaningful.</returns>
        public static ClaimsPrincipal FromCallerHeaders(string? adminHeader, string? callerIdHeader)
        {
            var identity = new ClaimsIdentity(authenticationType: "Header");

            if (string.Equals(adminHeader, "true", StringComparison.OrdinalIgnoreCase))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, AuthorizationRoles.Administrator));
            }

            if (!string.IsNullOrEmpty(callerIdHeader))
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, callerIdHeader));
            }

            return new ClaimsPrincipal(identity);
        }
    }

    private static HttpMappingOptions OptionsOf(HttpContext http) =>
        http.RequestServices.GetService<IOptions<HttpMappingOptions>>()?.Value
        ?? new HttpMappingOptions();

    private static ObjectResult BuildProblem(
        HttpContext http,
        int statusCode,
        string? title,
        string? detail,
        params (string Name, object Value)[] extensions
    )
    {
        var problem = http
            .RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateProblemDetails(http, statusCode, title, type: null, detail: detail);
        problem.Type = OptionsOf(http).TypeUriFor(statusCode);

        foreach (var (name, value) in extensions)
        {
            problem.Extensions[name] = value;
        }

        return AsProblemResult(problem);
    }

    private static ObjectResult AsProblemResult(ProblemDetails problem) =>
        new(problem) { StatusCode = problem.Status, ContentTypes = { ProblemJson } };
}

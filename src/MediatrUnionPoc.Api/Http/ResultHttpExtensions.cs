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

    /// <summary>The <see cref="CodeExtensionName"/> value on a 429 the rate limiter answers.</summary>
    public const string RateLimitedCode = "RATE_LIMITED";

    /// <summary>The <see cref="CodeExtensionName"/> value on a 504 the request timeout answers.</summary>
    public const string RequestTimeoutCode = "REQUEST_TIMEOUT";

    private const string ProblemJson = "application/problem+json";

    private const string MissingIfMatchDetail =
        "This request must be conditional: send an If-Match header carrying the ETag of the version you are changing.";

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

    extension(PreconditionFailed preconditionFailed)
    {
        /// <summary>Builds a 412 problem response whose detail is the case's message.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="statusCode">Overrides the 412 status for this call.</param>
        /// <param name="title">Overrides the title (default: <c>"Precondition Failed"</c>).</param>
        /// <param name="detail">Overrides the detail (default: the case's message).</param>
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
                statusCode ?? StatusCodes.Status412PreconditionFailed,
                title ?? "Precondition Failed",
                detail ?? preconditionFailed.Message
            );
        }
    }

    extension(Conflict conflict)
    {
        /// <summary>Builds a 409 problem response whose detail is the case's message.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="statusCode">Overrides the 409 status for this call.</param>
        /// <param name="title">Overrides the title (default: <c>"Conflict"</c>).</param>
        /// <param name="detail">Overrides the detail (default: the case's message).</param>
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
                statusCode ?? StatusCodes.Status409Conflict,
                title ?? "Conflict",
                detail ?? conflict.Message
            );
        }
    }

    extension(MissingIfMatch missingIfMatch)
    {
        /// <summary>Builds a 428 problem response telling the client the request must be conditional.</summary>
        /// <param name="http">The current request context.</param>
        /// <param name="statusCode">Overrides the 428 status for this call.</param>
        /// <param name="title">Overrides the title (default: <c>"Precondition Required"</c>).</param>
        /// <param name="detail">Overrides the detail (default: asks for an <c>If-Match</c> header carrying the ETag from a prior GET).</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>application/problem+json</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
        public IActionResult ToProblemResult(
            HttpContext http,
            int? statusCode = null,
            string? title = null,
            string? detail = null
        )
        {
            ArgumentNullException.ThrowIfNull(missingIfMatch);
            ArgumentNullException.ThrowIfNull(http);

            return BuildProblem(
                http,
                statusCode ?? StatusCodes.Status428PreconditionRequired,
                title ?? "Precondition Required",
                detail ?? MissingIfMatchDetail
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

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using MediatrUnionPoc.Api.Audit;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Api.Cors;
using MediatrUnionPoc.Api.Health;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.Impersonation;
using MediatrUnionPoc.Api.Logging;
using MediatrUnionPoc.Api.OpenApi;
using MediatrUnionPoc.Api.Proxies;
using MediatrUnionPoc.Api.RateLimiting;
using MediatrUnionPoc.Application;
using MediatrUnionPoc.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseApiLogging();

// This project's async methods keep their "Async" suffix by convention; ASP.NET Core's default
// (SuppressAsyncSuffixInActionNames = true) would otherwise register CreateAsync/GetByIdAsync/etc.
// as action names with the suffix trimmed (e.g. "GetById"), silently breaking any nameof(...)
// reference — such as Url.Action(nameof(GetByIdAsync), ...) — used for link generation.
builder
    .Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory())
    );

// URL-segment versioning: /api/v{version}/... . Requests without a version segment (the transitional
// unversioned aliases) are treated as the default version, 1.0. Every versioned response reports
// api-supported-versions; a rejected version is answered through IProblemDetailsService, so it is
// application/problem+json with the traceId like every other error.
builder
    .Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddVersionedOpenApi(ApiVersions.V1DocumentName);

builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddResultHttpMapping();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthEndpoints();
builder.Services.AddApiCors();
builder.Services.AddJwtAuthentication();
builder.Services.AddImpersonation();
builder.Services.AddAudit();
builder.Services.AddApiForwardedHeaders();
builder.Services.AddApiRateLimiting();

var app = builder.Build();

await app.Services.EnsureInfrastructureCreatedAsync();

if (app.Environment.IsDevelopment())
{
    // The documents stay anonymous in Development so the UI can load before a token is entered.
    app.MapOpenApi().AllowAnonymous().DisableRateLimiting();

    // One entry per API version, so the UI's document picker lists /openapi/v1.json, /openapi/v2.json, ...
    app.MapScalarApiReference(options =>
            options.AddDocuments(app.DescribeApiVersions().Select(version => version.GroupName))
        )
        .AllowAnonymous()
        .DisableRateLimiting();
}

// First: with trusted proxies configured, everything after it (rate limiting, request logging, the
// audit source address) sees the client's address instead of the proxy's. With none configured this
// adds nothing and a client's X-Forwarded-* headers are ignored.
app.UseApiForwardedHeaders();

app.UseMiddleware<TraceIdMiddleware>();

// Outside the exception handler: the one request line reports the final status, and a handled
// exception is logged once, at Error, by the handler itself.
app.UseApiRequestLogging();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseHttpsRedirection();

// After routing (implicit in the minimal host) and before authentication: a preflight carries no
// credentials and is answered here, never reaching the fallback authorization policy.
app.UseApiCors();

app.UseAuthentication();

app.UseUserLogContext();

// After authentication (it reads the principal) and before authorization, so a 403 an
// impersonated caller receives is recorded too.
app.UseImpersonationAudit();

// After authentication (the caller is the partition), CORS (a preflight is answered there and
// neither spends nor is refused by a budget) and the impersonation audit (a request refused with a
// 429 under an impersonation token is still recorded), and before authorization, so a request
// answered 401 or 403 still spends budget.
app.UseApiRateLimiting();

app.UseAuthorization();

// Every controller action is rate limited: [EnableRateLimiting] picks a policy, and an action that
// names none gets the Reads policy. Exempting an endpoint takes an explicit DisableRateLimiting.
app.MapControllers().WithDefaultRateLimiting();

app.MapHealthEndpoints();

await app.RunAsync();

/// <summary>
/// Gives <c>WebApplicationFactory&lt;Program&gt;</c> (used by the Api integration tests) something
/// accessible to reference — a top-level-statement Program is otherwise an internal, unnamed type.
/// Sonar's S1118 ("add a protected constructor or make it static") doesn't apply: this shape is
/// the standard, documented ASP.NET Core pattern for exposing the entry point to
/// <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;

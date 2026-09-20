using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Api.Health;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.OpenApi;
using MediatrUnionPoc.Application;
using MediatrUnionPoc.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// This project's async methods keep their "Async" suffix by convention; ASP.NET Core's default
// (SuppressAsyncSuffixInActionNames = true) would otherwise register CreateAsync/GetByIdAsync/etc.
// as action names with the suffix trimmed (e.g. "GetById"), silently breaking any nameof(...)
// reference — such as CreatedAtAction(nameof(GetByIdAsync), ...) — used for link generation.
builder
    .Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory())
    );

builder.Services.AddOpenApi(options =>
{
    options.CreateSchemaReferenceId = OptionalSchemaTransformer.CreateSchemaReferenceId;
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddSchemaTransformer<OptionalSchemaTransformer>();
    options.AddSchemaTransformer<ProductContractExampleTransformer>();
    options.AddOperationTransformer<ConsumesMediaTypeTransformer>();
    options.AddOperationTransformer<ETagResponseHeaderTransformer>();
    options.AddOperationTransformer<PagingResponseHeaderTransformer>();
    options.AddOperationTransformer<PreconditionProblemExampleTransformer>();
});

builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddResultHttpMapping();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthEndpoints();
builder.Services.AddJwtAuthentication();

var app = builder.Build();

await app.Services.EnsureInfrastructureCreatedAsync();

if (app.Environment.IsDevelopment())
{
    // The documents stay anonymous in Development so the UI can load before a token is entered.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseMiddleware<TraceIdMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

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

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

var app = builder.Build();

await app.Services.EnsureInfrastructureCreatedAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<TraceIdMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

/// <summary>
/// Gives <c>WebApplicationFactory&lt;Program&gt;</c> (used by the Api integration tests) something
/// accessible to reference — a top-level-statement Program is otherwise an internal, unnamed type.
/// Sonar's S1118 ("add a protected constructor or make it static") doesn't apply: this shape is
/// the standard, documented ASP.NET Core pattern for exposing the entry point to
/// <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;

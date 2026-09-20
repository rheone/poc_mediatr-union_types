using Microsoft.AspNetCore.Mvc.Testing;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Boots the real ASP.NET Core host (real DI container, real MediatR pipeline, real controller
/// routing, real SQLite). Nothing is substituted: with no <c>ConnectionStrings:Products</c> value
/// each host gets its own private in-memory SQLite database (kept alive by one open connection for
/// the host's lifetime) whose schema is created at startup, so tests using their own factory never
/// see another test's data.
/// </summary>
public sealed class ProductsApiFactory : WebApplicationFactory<Program>;

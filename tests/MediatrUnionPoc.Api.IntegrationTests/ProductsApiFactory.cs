using MediatrUnionPoc.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Boots the real ASP.NET Core host (real DI container, real MediatR pipeline, real controller
/// routing) with one substitution: a uniquely-named InMemory database per factory instance, so
/// tests using their own factory never see another test's data. <see cref="AppDbContext"/>'s
/// production registration (a single fixed database name) is intentionally shared across requests
/// within one running app — correct for the app, wrong for isolated tests.
/// </summary>
public sealed class ProductsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>Replaces the app's <see cref="AppDbContext"/> registration with one pointed at this factory's uniquely-named InMemory database.</summary>
    /// <param name="builder">The host builder being configured for the test server.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
            );
        });
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the opt-in development identity (<c>Authentication:DevIdentity</c>): off by default,
/// signs a tokenless request in as the configured user in Development, never overrides a real or
/// invalid token, follows a live change to its options, and never applies outside Development.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DevIdentityTests : IDisposable
{
    private const string ProductsUri = ApiRoutes.Products;
    private const string DevUser = "alice";
    private const string AdminUser = "root";
    private const string Administrator = "Administrator";
    private const string DevIdentityUserIdSetting = "Authentication:DevIdentity:UserId";

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies that with nothing configured a tokenless request is refused with 401, as before.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_NoDevIdentityConfigured_TokenlessRequestReturns401_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a configured development user signs a tokenless request in, so a request the fallback policy would refuse is answered 200.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_DevIdentityConfigured_TokenlessRequestIsSignedIn_Test()
    {
        // Arrange
        using var factory = WithDevIdentity(DevUser);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies the development user carries the configured roles: without Administrator DELETE is 403, with it 204.</summary>
    /// <param name="roles">The configured role, or an empty string for none.</param>
    /// <param name="expected">The status DELETE answers.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("", HttpStatusCode.Forbidden)]
    [InlineData(Administrator, HttpStatusCode.NoContent)]
    public async Task Delete_DevIdentityRoles_DecideTheOutcome_Test(
        string roles,
        HttpStatusCode expected
    )
    {
        // Arrange
        using var factory = WithDevIdentity(AdminUser, roles);
        using var client = factory.CreateClient();
        var id = await CreateProductAsync(client);

        // Act
        using var response = await client.DeleteAsync(
            $"{ProductsUri}/{id}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies an invalid token is refused even while a development user is configured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_DevIdentityConfigured_InvalidTokenStillReturns401_Test()
    {
        // Arrange
        using var factory = WithDevIdentity(DevUser);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "not-a-token"
        );

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a valid token is judged as the token says, not replaced by the development user.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_DevIdentityAdministrator_ValidNonAdminTokenStillReturns403_Test()
    {
        // Arrange
        using var factory = WithDevIdentity(AdminUser, Administrator);
        using var devClient = factory.CreateClient();
        var id = await CreateProductAsync(devClient);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.Create(_factory, "bob")
        );

        // Act
        using var response = await client.DeleteAsync(
            $"{ProductsUri}/{id}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a change to the monitored options applies to the very next request, on and off again, with no restart.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_DevIdentityChangedWhileRunning_AppliesToTheNextRequest_Test()
    {
        // Arrange
        var monitor = new MutableOptionsMonitor(new DevIdentityOptions());
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Replace(
                    ServiceDescriptor.Singleton<IOptionsMonitor<DevIdentityOptions>>(monitor)
                )
            )
        );
        using var client = factory.CreateClient();

        // Act
        using var before = await client.GetAsync(ProductsUri, CancellationToken.None);
        monitor.Value = new DevIdentityOptions { UserId = DevUser };
        using var during = await client.GetAsync(ProductsUri, CancellationToken.None);
        monitor.Value = new DevIdentityOptions();
        using var after = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, during.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    /// <summary>Verifies a host outside Development refuses to start when a development user is configured.</summary>
    [Fact]
    public void Start_NonDevelopmentWithDevIdentity_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = NonDevelopment(builder =>
            builder.UseSetting(DevIdentityUserIdSetting, DevUser)
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains("Development", StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies a value that appears after a non-Development host started is ignored: the request stays 401.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_NonDevelopmentWithDevIdentityAppearingLater_IsIgnored_Test()
    {
        // Arrange
        var monitor = new MutableOptionsMonitor(new DevIdentityOptions { UserId = DevUser });
        using var factory = NonDevelopment(builder =>
            builder.ConfigureTestServices(services =>
                services.Replace(
                    ServiceDescriptor.Singleton<IOptionsMonitor<DevIdentityOptions>>(monitor)
                )
            )
        );
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a null, empty or whitespace-only user id leaves the development identity off, so a tokenless request stays 401.</summary>
    /// <param name="userId">The configured user id, which names no user.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("	")]
    public async Task Get_DevIdentityUserIdBlank_TokenlessRequestReturns401_Test(string userId)
    {
        // Arrange
        using var factory = WithDevIdentity(userId);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies each constructor argument of <see cref="ConfigureDevIdentity"/> is guarded against <see langword="null"/>.</summary>
    /// <param name="nullArgument">The name of the constructor argument passed as <see langword="null"/>.</param>
    [Theory]
    [InlineData("options")]
    [InlineData("environment")]
    [InlineData("logger")]
    public void ConfigureDevIdentity_NullConstructorArgument_ThrowsArgumentNullException_Test(
        string nullArgument
    )
    {
        // Arrange
        var options = new MutableOptionsMonitor(new DevIdentityOptions());
        var environment = _factory.Services.GetRequiredService<IHostEnvironment>();
        var logger = NullLogger<ConfigureDevIdentity>.Instance;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ConfigureDevIdentity(
                nullArgument == "options" ? null! : options,
                nullArgument == "environment" ? null! : environment,
                nullArgument == "logger" ? null! : logger
            )
        );

        // Assert
        Assert.Equal(nullArgument, exception.ParamName);
    }

    /// <summary>Verifies <see cref="ConfigureDevIdentity.Configure(string?, JwtBearerOptions)"/> rejects <see langword="null"/> bearer options.</summary>
    [Fact]
    public void ConfigureDevIdentity_NullJwtBearerOptions_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var configure = new ConfigureDevIdentity(
            new MutableOptionsMonitor(new DevIdentityOptions()),
            _factory.Services.GetRequiredService<IHostEnvironment>(),
            NullLogger<ConfigureDevIdentity>.Instance
        );

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            configure.Configure(JwtBearerDefaults.AuthenticationScheme, null!)
        );

        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    /// <summary>Verifies <see cref="DevIdentityEnvironmentValidator"/> guards its constructor argument and the options it validates against <see langword="null"/>.</summary>
    [Fact]
    public void DevIdentityEnvironmentValidator_NullArguments_ThrowArgumentNullException_Test()
    {
        // Arrange
        var validator = new DevIdentityEnvironmentValidator(
            _factory.Services.GetRequiredService<IHostEnvironment>()
        );

        // Act
        var constructor = Assert.Throws<ArgumentNullException>(() =>
            new DevIdentityEnvironmentValidator(null!)
        );
        var validate = Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));

        // Assert
        Assert.Equal("environment", constructor.ParamName);
        Assert.Equal("options", validate.ParamName);
    }

    private WebApplicationFactory<Program> WithDevIdentity(string userId, string roles = "") =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(DevIdentityUserIdSetting, userId);
            var index = 0;
            foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                builder.UseSetting($"Authentication:DevIdentity:Roles:{index++}", role);
            }
        });

    private WebApplicationFactory<Program> NonDevelopment(Action<IWebHostBuilder> configure) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder
                .UseEnvironment("Production")
                .UseSetting("Authentication:Jwt:SigningKey", JwtTestTokens.NonDevelopmentSigningKey)
                .UseSetting(
                    "Impersonation:SigningKey",
                    JwtTestTokens.NonDevelopmentImpersonationKey
                );
            configure(builder);
        });

    private static async Task<string> CreateProductAsync(HttpClient client)
    {
        using var created = await client.PostAsJsonAsync(
            ProductsUri,
            new { name = "Dev product", price = 1.5m },
            CancellationToken.None
        );
        created.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync(CancellationToken.None)
        );
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private sealed class MutableOptionsMonitor(DevIdentityOptions initial)
        : IOptionsMonitor<DevIdentityOptions>
    {
        public DevIdentityOptions Value { get; set; } = initial;

        public DevIdentityOptions CurrentValue => Value;

        public DevIdentityOptions Get(string? name) => Value;

        public IDisposable? OnChange(Action<DevIdentityOptions, string?> listener) => null;
    }
}

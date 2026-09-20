using System.Net;
using System.Security.Claims;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the configurable union-to-HTTP mapping: <see cref="HttpMappingOptions"/> registered
/// in a real host changing a response, and the public extension members called directly with a
/// <see cref="DefaultHttpContext"/> to prove per-call overrides.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResultHttpMappingTests
{
    private const string ProductsUri = "/api/products";

    /// <summary>Verifies a host-registered error-code mapping changes the status the API answers with for that code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AddResultHttpMapping_ValidationFailureCodeRemapped_ApiRespondsWithMappedStatus_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddResultHttpMapping(options =>
                    options.ErrorStatusCodes[Error.ValidationFailureCode] =
                        StatusCodes.Status422UnprocessableEntity
                )
            )
        );
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(
            $"{ProductsUri}/{Guid.Empty}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>Verifies an error whose code the host mapped to a custom status answers with it.</summary>
    [Fact]
    public void ToProblemResult_ErrorWithCustomCode_UsesMappedStatus_Test()
    {
        // Arrange
        using var provider = BuildProvider(options =>
            options.ErrorStatusCodes["OUT_OF_STOCK"] = 503
        );
        var http = new DefaultHttpContext { RequestServices = provider };

        // Act
        var result = new Error("Try later", "OUT_OF_STOCK").ToProblemResult(http);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, Assert.IsType<ProblemDetails>(objectResult.Value).Status);
    }

    /// <summary>Verifies an error with no mapped code answers 500 by default.</summary>
    [Fact]
    public void ToProblemResult_ErrorWithUnmappedCode_Returns500_Test()
    {
        // Arrange
        using var provider = BuildProvider();
        var http = new DefaultHttpContext { RequestServices = provider };

        // Act
        var result = new Error("boom", "SOMETHING").ToProblemResult(http);

        // Assert
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Multiple(
            () => Assert.Equal(500, problem.Status),
            () => Assert.Equal("SOMETHING", problem.Title),
            () => Assert.Equal("boom", problem.Detail)
        );
    }

    /// <summary>Verifies a per-call status override wins over the shared table.</summary>
    [Fact]
    public void ToProblemResult_ErrorWithStatusOverride_OverrideWins_Test()
    {
        // Arrange
        using var provider = BuildProvider();
        var http = new DefaultHttpContext { RequestServices = provider };

        // Act
        var result = new Error("nope", Error.ValidationFailureCode).ToProblemResult(
            http,
            statusCode: 409,
            title: "Clash"
        );

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Multiple(
            () => Assert.Equal(409, objectResult.StatusCode),
            () => Assert.Equal(409, problem.Status),
            () => Assert.Equal("Clash", problem.Title)
        );
    }

    /// <summary>Verifies switching type URIs off omits the <c>type</c> member.</summary>
    [Fact]
    public void ToProblemResult_TypeUrisDisabled_OmitsType_Test()
    {
        // Arrange
        using var provider = BuildProvider(options => options.IncludeTypeUris = false);
        var http = new DefaultHttpContext { RequestServices = provider };

        // Act
        var result = new NotAuthorized(["no"]).ToProblemResult(http);

        // Assert
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Null(problem.Type);
    }

    /// <summary>Verifies the default policy emits the RFC 7231 type URI for the status.</summary>
    [Fact]
    public void ToProblemResult_NotAuthorizedDefaults_Returns403WithTypeUri_Test()
    {
        // Arrange
        using var provider = BuildProvider();
        var http = new DefaultHttpContext { RequestServices = provider };

        // Act
        var result = new NotAuthorized(["a", "b"]).ToProblemResult(http);

        // Assert
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Multiple(
            () => Assert.Equal(403, problem.Status),
            () => Assert.Equal("Forbidden", problem.Title),
            () => Assert.Equal("a; b", problem.Detail),
            () => Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.3", problem.Type)
        );
    }

    /// <summary>Verifies the not-found extension honours a status override yet keeps the NOT_FOUND code.</summary>
    [Fact]
    public void ToProblemResult_NotFoundWithStatusOverride_KeepsCodeAndUsesStatus_Test()
    {
        // Arrange
        using var provider = BuildProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var id = new Guid("11111111-1111-1111-1111-111111111111");
        var notFound = new NotFound<ProductId>(ProductId.From(id));

        // Act
        var result = notFound.ToProblemResult(http, resource: "Product", statusCode: 410);

        // Assert
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Multiple(
            () => Assert.Equal(410, problem.Status),
            () => Assert.Equal("NOT_FOUND", problem.Extensions["code"]),
            () =>
                Assert.Equal(
                    "Product '11111111-1111-1111-1111-111111111111' was not found.",
                    problem.Detail
                )
        );
    }

    /// <summary>Verifies validation errors are grouped per property in the validation problem.</summary>
    [Fact]
    public void ToProblemResult_ValidationErrors_GroupsMessagesPerProperty_Test()
    {
        // Arrange
        using var provider = BuildProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var errors = new ValidationErrors([
            new ValidationError("Name", "too short"),
            new ValidationError("Name", "bad chars"),
        ]);

        // Act
        var result = errors.ToProblemResult(http);

        // Assert
        var problem = Assert.IsType<ValidationProblemDetails>(
            Assert.IsType<ObjectResult>(result).Value
        );
        Assert.Multiple(
            () => Assert.Equal(400, problem.Status),
            () => Assert.Equal(["too short", "bad chars"], problem.Errors["Name"])
        );
    }

    /// <summary>Verifies the caller principal grants the admin role and identity only from the matching headers.</summary>
    [Fact]
    public void FromCallerHeaders_AdminAndCallerId_BuildsClaims_Test()
    {
        // Arrange / Act
        var principal = ClaimsPrincipal.FromCallerHeaders("TRUE", "alice");
        var anonymous = ClaimsPrincipal.FromCallerHeaders("false", string.Empty);

        // Assert
        Assert.Multiple(
            () => Assert.True(principal.IsInRole("Administrator")),
            () => Assert.Equal("alice", principal.FindFirstValue(ClaimTypes.NameIdentifier)),
            () => Assert.Empty(anonymous.Claims)
        );
    }

    private static ServiceProvider BuildProvider(Action<HttpMappingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddResultHttpMapping(configure);

        return services.BuildServiceProvider();
    }
}

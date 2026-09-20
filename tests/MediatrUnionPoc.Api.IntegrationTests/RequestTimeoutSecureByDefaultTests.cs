using MediatrUnionPoc.Api.RequestTimeouts;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Proves request timeouts are on by default: no endpoint in the host is silently exempt (a walk over every
/// mapped endpoint), the only exemptions are the operational endpoints (health probes and the Development
/// documents), and the impersonation endpoint is on its own, shorter policy. The behavioural proof that an
/// action with no attribute is timed out is in <c>RequestTimeoutTests</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RequestTimeoutSecureByDefaultTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies no controller action is exempt from timeouts and only the impersonation action names a policy.</summary>
    [Fact]
    public void EveryControllerEndpoint_IsCovered_AndOnlyMintingNamesAPolicy_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var actions = Endpoints()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .Select(e =>
                (
                    Pattern: e.RoutePattern.RawText!,
                    Policy: e.Metadata.GetMetadata<RequestTimeoutAttribute>()?.PolicyName,
                    Exempt: e.Metadata.GetMetadata<DisableRequestTimeoutAttribute>() is not null
                )
            )
            .ToList();

        // Act / Assert
        Assert.Multiple(
            () => Assert.NotEmpty(actions),
            () => Assert.All(actions, action => Assert.False(action.Exempt, action.Pattern)),
            () =>
                Assert.All(
                    actions,
                    action =>
                    {
                        var minting = action.Pattern.EndsWith(
                            "/impersonation/tokens",
                            StringComparison.Ordinal
                        );
                        Assert.Equal(
                            minting ? RequestTimeoutPolicyNames.Impersonation : null,
                            action.Policy
                        );
                    }
                )
        );
    }

    /// <summary>Verifies every endpoint that opts out of timeouts is an operational one: a health probe or a Development document.</summary>
    [Fact]
    public void EveryExemptEndpoint_IsOperational_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var exempt = Endpoints()
            .Where(e => e.Metadata.GetMetadata<DisableRequestTimeoutAttribute>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .ToList();

        // Act / Assert
        Assert.Multiple(
            () => Assert.NotEmpty(exempt),
            () =>
                Assert.All(
                    exempt,
                    pattern =>
                        Assert.True(
                            pattern.StartsWith("/health/", StringComparison.Ordinal)
                                || pattern.StartsWith("/openapi/", StringComparison.Ordinal)
                                || pattern.StartsWith("/scalar", StringComparison.Ordinal),
                            $"{pattern} is exempt from timeouts but is not an operational endpoint."
                        )
                )
        );
    }

    /// <summary>Verifies the framework carries a default policy (what covers an action nobody annotated) and the named policy, both answering 504.</summary>
    [Fact]
    public void FrameworkOptions_HaveADefaultAndANamedPolicy_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var options = _factory
            .Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutOptions>>()
            .Value;

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(options.DefaultPolicy),
            () => Assert.Equal(504, options.DefaultPolicy!.TimeoutStatusCode),
            () =>
                Assert.Equal(
                    504,
                    options.Policies[RequestTimeoutPolicyNames.Impersonation].TimeoutStatusCode
                )
        );
    }

    private List<RouteEndpoint> Endpoints() =>
        [
            .. _factory
                .Services.GetServices<EndpointDataSource>()
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>(),
        ];
}

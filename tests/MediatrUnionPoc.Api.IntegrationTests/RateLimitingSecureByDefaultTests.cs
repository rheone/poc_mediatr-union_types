using System.Net;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Api.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Proves the limiter is on by default: an action added later with no rate-limiting attribute at all is
/// limited, a declared policy beats that default, an explicit exemption is honoured, and no endpoint in the
/// host is silently unlimited (a walk over every mapped endpoint).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingSecureByDefaultTests : IDisposable
{
    private static readonly string[] MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies an action with no attribute is limited by the Reads policy, an action naming Writes gets the Writes budget (the default never overrides a declaration), and an action with DisableRateLimiting is never limited.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ANewActionWithoutAnyAttribute_IsLimited_AndDeclarationsAreHonoured_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(
            reads: 2,
            writes: 4,
            configure: builder =>
                builder.ConfigureTestServices(services =>
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(RateLimitProbeController).Assembly)
                )
        );
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        var unannotated = await client.GetStatusesAsync("/probe/unannotated", 3);
        var declared = await client.GetStatusesAsync("/probe/writes", 5);
        var exempt = await client.GetStatusesAsync("/probe/exempt", 10);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 200, 429], unannotated),
            () => Assert.Equal([200, 200, 200, 200, 429], declared),
            () => Assert.All(exempt, status => Assert.Equal(200, status))
        );
    }

    /// <summary>Verifies every real controller action has an explicit or defaulted policy, none is exempt, and the policy fits the verb: reads on Reads, product mutations on Writes, token minting on Impersonation.</summary>
    [Fact]
    public void EveryControllerEndpoint_HasAPolicyThatFitsItsVerb_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var actions = Endpoints()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();

        // Act
        var policies = actions
            .Select(e =>
                (
                    Pattern: e.RoutePattern.RawText!,
                    Verbs: e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods,
                    Policy: e.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
                    Exempt: e.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null
                )
            )
            .ToList();

        // Assert
        Assert.Multiple(
            () => Assert.NotEmpty(policies),
            () => Assert.All(policies, action => Assert.False(action.Exempt, action.Pattern)),
            () =>
                Assert.All(
                    policies,
                    action =>
                    {
                        var mutating = action.Verbs.Any(v => MutatingMethods.Contains(v));
                        var minting = action.Pattern.EndsWith(
                            "/impersonation/tokens",
                            StringComparison.Ordinal
                        );
                        var expected = mutating
                            ? RateLimitPolicyNames.Writes
                            : RateLimitPolicyNames.Reads;
                        if (minting)
                        {
                            expected = RateLimitPolicyNames.Impersonation;
                        }

                        Assert.True(
                            expected == action.Policy,
                            $"{string.Join('/', action.Verbs)} {action.Pattern} is on '{action.Policy}', expected '{expected}'."
                        );
                    }
                )
        );
    }

    /// <summary>Verifies no endpoint of the host is unlimited by accident: each one either declares a policy or is one of the explicitly exempt operational endpoints (health probes and the Development documents).</summary>
    [Fact]
    public void EveryEndpoint_IsLimitedOrExplicitlyExempt_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var undeclared = Endpoints()
            .Where(e =>
                e.Metadata.GetMetadata<EnableRateLimitingAttribute>() is null
                && e.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null
            )
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        var exempt = Endpoints()
            .Where(e => e.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .ToList();

        // Assert
        Assert.Multiple(
            () => Assert.Empty(undeclared),
            () => Assert.NotEmpty(exempt),
            () =>
                Assert.All(
                    exempt,
                    pattern =>
                        Assert.True(
                            pattern.StartsWith("/health/", StringComparison.Ordinal)
                                || pattern.StartsWith("/openapi/", StringComparison.Ordinal)
                                || pattern.StartsWith("/scalar", StringComparison.Ordinal),
                            $"{pattern} is exempt from rate limiting but is not an operational endpoint."
                        )
                )
        );
    }

    /// <summary>Verifies the limiter is really in the pipeline for an unmatched route: it answers the ordinary 404 (or 401) rather than a 500, so a request that matches nothing is not a way to break the host.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnknownRoute_StillAnswersNormally_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync("/no/such/route", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private List<RouteEndpoint> Endpoints() =>
        [
            .. _factory
                .Services.GetServices<EndpointDataSource>()
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>(),
        ];
}

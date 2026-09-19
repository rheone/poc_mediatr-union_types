using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// A second, unrelated handler for the same <see cref="OperationAuthorizationRequirement"/> as
/// <see cref="OwnerAuthorizationHandler{TResource}"/> — succeeds when the caller carries a
/// "SupportOverride" claim, regardless of resource ownership. Exists only to prove that multiple
/// handlers registered for one requirement are evaluated OR-across-handlers by the framework
/// itself, the same way <see cref="AdministratorAuthorizationHandler"/> already ORs across roles
/// within a single handler.
/// </summary>
/// <typeparam name="TResource">The resource type the requirement is being evaluated against.</typeparam>
public sealed class OverrideClaimAuthorizationHandler<TResource>
    : AuthorizationHandler<OperationAuthorizationRequirement, TResource>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        TResource resource
    )
    {
        if (context.User.HasClaim("SupportOverride", "true"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Verifies that registering two <see cref="IAuthorizationHandler"/> instances for the same
/// resource requirement is evaluated OR-across-handlers — either one succeeding is enough to
/// satisfy the policy, exactly as multiple requirements within one policy are AND'd. No new
/// fan-out mechanism is needed for this; it already works via the framework's own handler
/// registration and <c>PendingRequirements</c> evaluation.
/// </summary>
public sealed class ResourceAuthorizationOrAcrossHandlersTests : IDisposable
{
    private const string PolicyName = "TestResourceUpdate";
    private const string OwnerId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly ServiceProvider _provider;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> backed by two competing handlers for one policy.</summary>
    public ResourceAuthorizationOrAcrossHandlersTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                PolicyName,
                policy =>
                    policy.Requirements.Add(
                        new OperationAuthorizationRequirement { Name = "Update" }
                    )
            )
        );
        services.AddSingleton<IAuthorizationHandler, OwnerAuthorizationHandler<TestResource>>();
        services.AddSingleton<
            IAuthorizationHandler,
            OverrideClaimAuthorizationHandler<TestResource>
        >();
        _provider = services.BuildServiceProvider();
        _authorizationService = _provider.GetRequiredService<IAuthorizationService>();
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies ownership alone, via <see cref="OwnerAuthorizationHandler{TResource}"/>, is enough to succeed.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_owning_caller_without_override_claim_succeeds()
    {
        // Arrange
        var resource = new TestResource(OwnerId);
        var principal = PrincipalMother.WithId(OwnerId);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, resource, PolicyName);

        // Assert
        Assert.True(result.Succeeded);
    }

    /// <summary>Verifies the override claim alone, via <see cref="OverrideClaimAuthorizationHandler{TResource}"/>, is enough to succeed even for a non-owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_non_owner_with_override_claim_succeeds()
    {
        // Arrange
        var resource = new TestResource(OwnerId);
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, OtherUserId),
                    new Claim("SupportOverride", "true"),
                ],
                authenticationType: "Test"
            )
        );

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, resource, PolicyName);

        // Assert
        Assert.True(result.Succeeded);
    }

    /// <summary>Verifies the policy fails when neither handler succeeds.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_non_owner_without_override_claim_fails()
    {
        // Arrange
        var resource = new TestResource(OwnerId);
        var principal = PrincipalMother.WithId(OtherUserId);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, resource, PolicyName);

        // Assert
        Assert.False(result.Succeeded);
    }
}

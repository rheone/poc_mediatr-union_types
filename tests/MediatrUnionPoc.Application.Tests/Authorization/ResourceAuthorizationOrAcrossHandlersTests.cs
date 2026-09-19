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
    /// <summary>The claim type whose value <see cref="ClaimValue"/> makes this handler succeed.</summary>
    public const string ClaimType = "SupportOverride";

    /// <summary>The claim value that makes this handler succeed when carried on <see cref="ClaimType"/>.</summary>
    public const string ClaimValue = "true";

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        TResource resource
    )
    {
        if (context.User.HasClaim(ClaimType, ClaimValue))
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
    private const string UpdateOperation = "Update";
    private const string AuthenticationType = "Test";

    // SWEEP-AMBIGUITY: the framework's AuthorizeAsync(principal, resource, policyName) is the member under test
    // and OwnerAuthorizationHandler has no null guards of its own (a null resource fails with a
    // NullReferenceException) / null arguments should throw ArgumentNullException, but no such test is written
    // because that behavior belongs to the framework and production does not add it.
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
                        new OperationAuthorizationRequirement { Name = UpdateOperation }
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
    public async Task AuthorizeAsync_OwningCallerWithoutOverrideClaim_Succeeds_Test()
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
    public async Task AuthorizeAsync_NonOwnerWithOverrideClaim_Succeeds_Test()
    {
        // Arrange
        var resource = new TestResource(OwnerId);
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, OtherUserId),
                    new Claim(
                        OverrideClaimAuthorizationHandler<TestResource>.ClaimType,
                        OverrideClaimAuthorizationHandler<TestResource>.ClaimValue
                    ),
                ],
                authenticationType: AuthenticationType
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
    public async Task AuthorizeAsync_NonOwnerWithoutOverrideClaim_Fails_Test()
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

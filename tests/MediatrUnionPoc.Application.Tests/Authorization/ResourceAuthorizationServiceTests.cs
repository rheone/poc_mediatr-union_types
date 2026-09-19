using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Verifies <see cref="ResourceAuthorizationService"/> calls the resource-aware
/// <see cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/> overload and
/// translates the outcome into a nullable <c>NotAuthorized</c> — leaving it to the caller to fold
/// a non-null result into its own union via
/// <see cref="MediatrUnionPoc.Application.Common.Abstractions.IAuthorizable{TSelf}"/>.
/// </summary>
public sealed class ResourceAuthorizationServiceTests
{
    private const string PolicyName = "SomePolicy";
    private const string ResourceOwnerId = "user-1";

    private readonly IAuthorizationService _authorizationService =
        Substitute.For<IAuthorizationService>();

    private readonly ResourceAuthorizationService _sut;

    /// <summary>Wires up <see cref="_sut"/> against the mocked <see cref="_authorizationService"/>.</summary>
    public ResourceAuthorizationServiceTests() =>
        _sut = new ResourceAuthorizationService(_authorizationService);

    /// <summary>Verifies a successful authorization result yields <see langword="null"/> rather than a <c>NotAuthorized</c>.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_SuccessfulAuthorization_ReturnsNull_Test()
    {
        // Arrange
        var principal = PrincipalMother.Anonymous();
        var resource = new TestResource(ResourceOwnerId);
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Success());

        // Act
        var result = await _sut.AuthorizeAsync(
            principal,
            resource,
            PolicyName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
        await _authorizationService.Received(1).AuthorizeAsync(principal, resource, PolicyName);
    }

    /// <summary>Verifies a failed authorization result yields a <c>NotAuthorized</c> naming the policy that was checked.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_FailedAuthorization_ReturnsNotAuthorizedNamingPolicy_Test()
    {
        // Arrange
        var principal = PrincipalMother.Anonymous();
        var resource = new TestResource(ResourceOwnerId);
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Failed());

        // Act
        var result = await _sut.AuthorizeAsync(
            principal,
            resource,
            PolicyName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains(PolicyName, result.Reasons.Single());
        await _authorizationService.Received(1).AuthorizeAsync(principal, resource, PolicyName);
    }

    /// <summary>Verifies the resource-aware three-argument overload is called with exactly the given principal, resource, and policy.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_AnyArguments_DelegatesToResourceAwareOverload_Test()
    {
        // Arrange
        var principal = PrincipalMother.Anonymous();
        var resource = new TestResource(ResourceOwnerId);
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Success());

        // Act
        await _sut.AuthorizeAsync(
            principal,
            resource,
            PolicyName,
            TestContext.Current.CancellationToken
        );

        // Assert
        await _authorizationService.Received(1).AuthorizeAsync(principal, resource, PolicyName);
    }

    /// <summary>Verifies the constructor rejects a null authorization service instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullAuthorizationService_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ResourceAuthorizationService(null!)
        );

        // Assert
        Assert.Equal("authorizationService", ex.ParamName);
    }

    /// <summary>Verifies a null principal is rejected with <see cref="ArgumentNullException"/> before the framework service is called.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task AuthorizeAsync_NullPrincipal_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.AuthorizeAsync(
                null!,
                new TestResource(ResourceOwnerId),
                PolicyName,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("principal", ex.ParamName);
        await _authorizationService
            .DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(default!, default, default(string)!);
    }

    /// <summary>Verifies a null resource is rejected with <see cref="ArgumentNullException"/> before the framework service is called.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task AuthorizeAsync_NullResource_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.AuthorizeAsync(
                PrincipalMother.Anonymous(),
                null!,
                PolicyName,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("resource", ex.ParamName);
        await _authorizationService
            .DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(default!, default, default(string)!);
    }

    /// <summary>Verifies a null policy name is rejected with <see cref="ArgumentNullException"/> before the framework service is called.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task AuthorizeAsync_NullPolicyName_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.AuthorizeAsync(
                PrincipalMother.Anonymous(),
                new TestResource(ResourceOwnerId),
                null!,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("policyName", ex.ParamName);
        await _authorizationService
            .DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(default!, default, default(string)!);
    }
}

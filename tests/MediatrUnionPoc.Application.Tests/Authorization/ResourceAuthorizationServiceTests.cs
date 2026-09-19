using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
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

    private readonly IAuthorizationService _authorizationService =
        Substitute.For<IAuthorizationService>();

    private readonly ResourceAuthorizationService _sut;

    /// <summary>Wires up <see cref="_sut"/> against the mocked <see cref="_authorizationService"/>.</summary>
    public ResourceAuthorizationServiceTests() =>
        _sut = new ResourceAuthorizationService(_authorizationService);

    /// <summary>Verifies a successful authorization result yields <see langword="null"/> rather than a <c>NotAuthorized</c>.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Successful_authorization_returns_null()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var resource = new TestResource("user-1");
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Success());

        var result = await _sut.AuthorizeAsync(principal, resource, PolicyName);

        Assert.Null(result);
    }

    /// <summary>Verifies a failed authorization result yields a <c>NotAuthorized</c> naming the policy that was checked.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Failed_authorization_returns_a_NotAuthorized_case()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var resource = new TestResource("user-1");
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Failed());

        var result = await _sut.AuthorizeAsync(principal, resource, PolicyName);

        Assert.NotNull(result);
        Assert.Contains(PolicyName, result.Reasons.Single());
    }

    /// <summary>Verifies the resource-aware three-argument overload is called with exactly the given principal, resource, and policy.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task AuthorizeAsync_calls_the_resource_aware_overload_with_the_given_arguments()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var resource = new TestResource("user-1");
        _authorizationService
            .AuthorizeAsync(principal, resource, PolicyName)
            .Returns(AuthorizationResult.Success());

        await _sut.AuthorizeAsync(principal, resource, PolicyName);

        await _authorizationService.Received(1).AuthorizeAsync(principal, resource, PolicyName);
    }
}

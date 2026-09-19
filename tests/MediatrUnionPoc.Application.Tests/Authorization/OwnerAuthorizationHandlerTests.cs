using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// A resource type with no relationship to any real domain entity — standing in for "a dev's own
/// arbitrary resource, never seen by <see cref="OwnerAuthorizationHandler{TResource}"/>'s author,"
/// the same role <c>SomeDevsOwnCaseType</c> plays in <c>TransactionBehaviorTests</c>.
/// </summary>
/// <param name="OwnerId">The identifier of the caller who owns this resource.</param>
public sealed record TestResource(string OwnerId) : IOwnedResource;

/// <summary>
/// Exercises <see cref="OwnerAuthorizationHandler{TResource}"/> directly against a real
/// <see cref="AuthorizationHandlerContext"/> — the same style <see cref="AdministratorAuthorizationHandlerTests"/>
/// uses, and the style ASP.NET Core's own resource-based authorization handlers are unit-tested with.
/// </summary>
public sealed class OwnerAuthorizationHandlerTests
{
    private readonly OwnerAuthorizationHandler<TestResource> _sut = new();

    /// <summary>Verifies the requirement succeeds when the caller's identifier claim matches the resource's owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Succeeds_when_the_caller_owns_the_resource()
    {
        var requirement = new OperationAuthorizationRequirement { Name = "Update" };
        var resource = new TestResource(OwnerId: "user-1");
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalWithId("user-1"),
            resource
        );

        await _sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>Verifies the requirement fails when the caller's identifier claim doesn't match the resource's owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Fails_when_the_caller_does_not_own_the_resource()
    {
        var requirement = new OperationAuthorizationRequirement { Name = "Update" };
        var resource = new TestResource(OwnerId: "user-1");
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalWithId("user-2"),
            resource
        );

        await _sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>Verifies the requirement fails, rather than throwing, when the caller has no identifier claim at all.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Fails_when_the_caller_has_no_identifier_claim()
    {
        var requirement = new OperationAuthorizationRequirement { Name = "Update" };
        var resource = new TestResource(OwnerId: "user-1");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource
        );

        await _sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static ClaimsPrincipal PrincipalWithId(string id)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)],
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
    }
}

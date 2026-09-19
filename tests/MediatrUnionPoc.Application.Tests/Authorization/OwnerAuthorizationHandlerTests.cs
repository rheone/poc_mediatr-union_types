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

    /// <summary>
    /// Verifies the requirement succeeds only when the caller's identifier claim matches the
    /// resource's owner exactly.
    /// </summary>
    /// <param name="callerId">The <see cref="ClaimTypes.NameIdentifier"/> claim value of the simulated caller.</param>
    /// <param name="expectedSuccess">Whether the requirement is expected to succeed for this caller.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Theory]
    [InlineData("user-1", true)]
    [InlineData("user-2", false)]
    public async Task Ownership_match_determines_authorization_result(
        string callerId,
        bool expectedSuccess
    )
    {
        var requirement = new OperationAuthorizationRequirement { Name = "Update" };
        var resource = new TestResource(OwnerId: "user-1");
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalWithId(callerId),
            resource
        );

        await _sut.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }

    /// <summary>Verifies the requirement fails, rather than throwing, when the caller has no identifier claim at all.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Caller_with_no_identifier_claim_fails_authorization()
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

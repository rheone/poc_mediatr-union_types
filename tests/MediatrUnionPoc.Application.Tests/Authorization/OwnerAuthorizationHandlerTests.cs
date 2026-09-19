using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
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
    private const string OwnerId = "user-1";
    private const string OtherUserId = "user-2";
    private const string UpdateOperation = "Update";

    // SWEEP-AMBIGUITY: HandleAsync(context) has no ArgumentNullException guard (a null context surfaces as a
    // NullReferenceException from the framework base class) / a null context should throw ArgumentNullException
    // naming "context", but no such test is written because production does not do that.
    private readonly OwnerAuthorizationHandler<TestResource> _sut = new();

    /// <summary>Verifies the requirement succeeds when the caller's identifier claim equals the resource's owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task HandleAsync_CallerIsOwner_Succeeds_Test()
    {
        // Arrange
        var context = ContextFor(PrincipalMother.WithId(OwnerId));

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    /// <summary>Verifies the requirement does not succeed when the caller's identifier claim differs from the resource's owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task HandleAsync_CallerIsNotOwner_Fails_Test()
    {
        // Arrange
        var context = ContextFor(PrincipalMother.WithId(OtherUserId));

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>Verifies the requirement fails, rather than throwing, when the caller has no identifier claim at all.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task HandleAsync_CallerWithoutIdentifierClaim_Fails_Test()
    {
        // Arrange
        var context = ContextFor(PrincipalMother.Anonymous());

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>Verifies ownership is compared case-sensitively — an identifier differing from the owner only by case is not the owner.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task HandleAsync_OwnerIdDifferingOnlyByCase_Fails_Test()
    {
        // Arrange
        var context = ContextFor(PrincipalMother.WithId(OwnerId.ToUpperInvariant()));

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext ContextFor(ClaimsPrincipal caller) =>
        new(
            [new OperationAuthorizationRequirement { Name = UpdateOperation }],
            caller,
            new TestResource(OwnerId)
        );
}

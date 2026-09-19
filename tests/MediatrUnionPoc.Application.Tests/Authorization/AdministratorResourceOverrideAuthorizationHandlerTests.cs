using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Exercises <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/> directly
/// against a real <see cref="AuthorizationHandlerContext"/> — the same style
/// <see cref="AdministratorAuthorizationHandlerTests"/> and <see cref="OwnerAuthorizationHandlerTests"/>
/// use. Only role membership and operation-name scoping are exercised here: the handler never
/// inspects the resource itself, so <see cref="TestResource"/> is reused as an arbitrary stand-in.
/// </summary>
public sealed class AdministratorResourceOverrideAuthorizationHandlerTests
{
    // SWEEP-AMBIGUITY: the ctor's allowedOperationNames array and HandleAsync(context) have no
    // ArgumentNullException guard (a null array or context fails later with a NullReferenceException) / each null
    // reference-type parameter should throw ArgumentNullException, but no such test is written because production
    // does not do that.
    private const string Administrator = "Administrator";
    private const string Viewer = "Viewer";
    private const string Delete = "Delete";
    private const string Update = "Update";

    /// <summary>
    /// Rows: administrator with an empty allowlist (unrestricted, succeeds); non-administrator with
    /// an empty allowlist (fails); administrator with a matching allowlisted operation (succeeds);
    /// administrator with a non-matching operation (fails); non-administrator with a matching
    /// allowlisted operation (fails, the role is still required).
    /// </summary>
    public static TheoryData<
        string[],
        string[],
        string,
        bool
    > HandleAsync_RoleAndOperationName_DeterminesAuthorizationResult_Test_Data =>
        new()
        {
            { [Administrator], [], Delete, true },
            { [Viewer], [], Delete, false },
            { [Administrator], [Delete], Delete, true },
            { [Administrator], [Delete], Update, false },
            { [Viewer], [Delete], Delete, false },
        };

    /// <summary>
    /// Verifies the requirement succeeds only for an Administrator caller, and that the handler's
    /// operation-name scoping further restricts that success: an empty allowlist applies to every
    /// operation name, while a non-empty allowlist blocks a requirement whose
    /// <see cref="OperationAuthorizationRequirement.Name"/> it doesn't contain.
    /// </summary>
    /// <param name="userRoles">The roles claimed by the simulated caller.</param>
    /// <param name="allowedOperationNames">The handler's configured operation-name allowlist.</param>
    /// <param name="requirementName">The <see cref="OperationAuthorizationRequirement.Name"/> under test.</param>
    /// <param name="expectedSuccess">Whether the requirement is expected to succeed for this combination.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Theory]
    [MemberData(nameof(HandleAsync_RoleAndOperationName_DeterminesAuthorizationResult_Test_Data))]
    public async Task HandleAsync_RoleAndOperationName_DeterminesAuthorizationResult_Test(
        string[] userRoles,
        string[] allowedOperationNames,
        string requirementName,
        bool expectedSuccess
    )
    {
        // Arrange
        var sut = new AdministratorResourceOverrideAuthorizationHandler<TestResource>(
            allowedOperationNames
        );
        var requirement = new OperationAuthorizationRequirement { Name = requirementName };
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalMother.WithRoles(userRoles),
            new TestResource(OwnerId: "user-1")
        );

        // Act
        await sut.HandleAsync(context);

        // Assert
        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }
}

using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Exercises <see cref="AdministratorAuthorizationHandler"/> directly against a real
/// <see cref="AuthorizationHandlerContext"/> — the same style ASP.NET Core's own built-in
/// <c>RolesAuthorizationHandler</c> is unit-tested with, since neither type needs a DI container
/// or an HTTP pipeline to exercise.
/// </summary>
public class AdministratorAuthorizationHandlerTests
{
    private const string Administrator = "Administrator";
    private const string SuperUser = "SuperUser";
    private const string Viewer = "Viewer";

    private readonly AdministratorAuthorizationHandler _sut = new();

    /// <summary>
    /// Rows: caller holds the only allowed role (succeeds); caller holds a different role (fails);
    /// caller holds one of several allowed roles, proving OR-across-roles (succeeds); no allowed
    /// roles configured (automatically succeeds); allowed roles configured but caller has none
    /// (fails).
    /// </summary>
    public static TheoryData<string[], string[], bool> HandleAsync_role_membership_Test_Data =>
        new()
        {
            { [Administrator], [Administrator], true },
            { [Administrator], [Viewer], false },
            { [Administrator, SuperUser], [SuperUser], true },
            { [], [], true },
            { [Administrator], [], false },
        };

    /// <summary>
    /// Verifies role matching is OR-across-roles — any one of the allowed roles is sufficient —
    /// and that an unconfigured requirement (no allowed roles) is automatically satisfied.
    /// </summary>
    /// <param name="allowedRoles">The requirement's configured <see cref="AdministratorRequirement.AllowedRoles"/>.</param>
    /// <param name="userRoles">The roles claimed by the simulated caller.</param>
    /// <param name="expectedSuccess">Whether the requirement is expected to succeed for this combination.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Theory]
    [MemberData(nameof(HandleAsync_role_membership_Test_Data))]
    public async Task HandleAsync_role_membership_determines_authorization_result(
        string[] allowedRoles,
        string[] userRoles,
        bool expectedSuccess
    )
    {
        // Arrange
        var requirement = new AdministratorRequirement(allowedRoles);
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalMother.WithRoles(userRoles),
            resource: null
        );

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }
}

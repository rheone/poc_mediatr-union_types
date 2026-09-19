using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Exercises <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/> directly
/// against a real <see cref="AuthorizationHandlerContext"/> — the same style
/// <see cref="AdministratorAuthorizationHandlerTests"/> and <see cref="OwnerAuthorizationHandlerTests"/>
/// use. Only role membership and operation-name scoping are exercised here: the handler never
/// inspects the resource itself, so <c>OwnerAuthorizationHandlerTests.TestResource</c> is reused as
/// an arbitrary stand-in.
/// </summary>
public sealed class AdministratorResourceOverrideAuthorizationHandlerTests
{
    /// <summary>
    /// Verifies the requirement succeeds only for an Administrator caller, and that
    /// <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/>'s operation-name
    /// scoping further restricts that success: an empty allowlist applies to every operation name
    /// (unrestricted), while a non-empty allowlist blocks a requirement whose
    /// <see cref="OperationAuthorizationRequirement.Name"/> it doesn't contain.
    /// </summary>
    /// <param name="userRoles">The roles claimed by the simulated caller.</param>
    /// <param name="allowedOperationNames">The handler's configured operation-name allowlist.</param>
    /// <param name="requirementName">The <see cref="OperationAuthorizationRequirement.Name"/> under test.</param>
    /// <param name="expectedSuccess">Whether the requirement is expected to succeed for this combination.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Theory]
    [InlineData(new[] { "Administrator" }, new string[0], "Delete", true)]
    [InlineData(new[] { "Viewer" }, new string[0], "Delete", false)]
    [InlineData(new[] { "Administrator" }, new[] { "Delete" }, "Delete", true)]
    [InlineData(new[] { "Administrator" }, new[] { "Delete" }, "Update", false)]
    public async Task Role_and_operation_name_scoping_determine_authorization_result(
        string[] userRoles,
        string[] allowedOperationNames,
        string requirementName,
        bool expectedSuccess
    )
    {
        var sut = new AdministratorResourceOverrideAuthorizationHandler<TestResource>(
            allowedOperationNames
        );
        var requirement = new OperationAuthorizationRequirement { Name = requirementName };
        var resource = new TestResource(OwnerId: "user-1");
        var context = new AuthorizationHandlerContext(
            [requirement],
            PrincipalWithRoles(userRoles),
            resource
        );

        await sut.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
    }
}

using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
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
    private readonly AdministratorAuthorizationHandler _sut = new();

    /// <summary>
    /// Verifies role matching is OR-across-roles — any one of the allowed roles is sufficient —
    /// and that an unconfigured requirement (no allowed roles) is automatically satisfied, the
    /// same "any match succeeds, no roles means nothing to check" semantics as ASP.NET Core's own
    /// built-in <c>RolesAuthorizationHandler</c>.
    /// </summary>
    /// <param name="allowedRoles">The requirement's configured <see cref="AdministratorRequirement.AllowedRoles"/>.</param>
    /// <param name="userRoles">The roles claimed by the simulated caller.</param>
    /// <param name="expectedSuccess">Whether the requirement is expected to succeed for this combination.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Theory]
    [InlineData(new[] { "Administrator" }, new[] { "Administrator" }, true)]
    [InlineData(new[] { "Administrator" }, new[] { "Viewer" }, false)]
    [InlineData(new[] { "Administrator", "SuperUser" }, new[] { "SuperUser" }, true)]
    [InlineData(new string[0], new string[0], true)]
    public async Task Role_membership_determines_authorization_result(
        string[] allowedRoles,
        string[] userRoles,
        bool expectedSuccess
    )
    {
        var requirement = new AdministratorRequirement(allowedRoles);
        var user = PrincipalWithRoles(userRoles);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

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

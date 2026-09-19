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

    /// <summary>Verifies the requirement succeeds when the caller holds one of its allowed roles.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Succeeds_when_the_user_has_one_of_the_allowed_roles()
    {
        var requirement = new AdministratorRequirement("Administrator");
        var user = PrincipalWithRoles("Administrator");
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>Verifies the requirement fails when the caller holds none of its allowed roles.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Fails_when_the_user_has_none_of_the_allowed_roles()
    {
        var requirement = new AdministratorRequirement("Administrator");
        var user = PrincipalWithRoles("Viewer");
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Verifies matching any one of several allowed roles is sufficient — the OR-across-roles
    /// semantics of ASP.NET Core's built-in <c>RolesAuthorizationRequirement</c>.
    /// </summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Succeeds_when_the_user_matches_at_least_one_of_several_allowed_roles()
    {
        var requirement = new AdministratorRequirement("Administrator", "SuperUser");
        var user = PrincipalWithRoles("SuperUser");
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// Verifies a requirement with no configured roles is automatically satisfied — there's
    /// nothing to challenge the caller against, mirroring the built-in handler's own behavior.
    /// </summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Succeeds_automatically_when_no_roles_are_configured()
    {
        var requirement = new AdministratorRequirement();
        var user = PrincipalWithRoles();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
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

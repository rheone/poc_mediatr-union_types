using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Evaluates the <c>Impersonator</c> policy exactly as <see cref="DependencyInjection.AddApplication"/>
/// registers it: satisfied by the <c>Administrator</c> or the <c>Support</c> role and nothing else,
/// and <c>Support</c> does not leak into the <c>Administrator</c> policy that guards deletes.
/// </summary>
public sealed class ImpersonatorPolicyTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>Builds the real production registration and resolves its <see cref="IAuthorizationService"/>.</summary>
    public ImpersonatorPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        _provider = services.BuildServiceProvider();
        _authorizationService = _provider.GetRequiredService<IAuthorizationService>();
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies which role sets satisfy the <c>Impersonator</c> policy.</summary>
    /// <param name="role">The single role held, or an empty string for none.</param>
    /// <param name="expected">Whether the policy should succeed.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Administrator", true)]
    [InlineData("Support", true)]
    [InlineData("Viewer", false)]
    [InlineData("administrator", false)]
    [InlineData("", false)]
    public async Task AuthorizeAsync_ImpersonatorPolicy_IsSatisfiedByAdministratorOrSupport_Test(
        string role,
        bool expected
    )
    {
        // Arrange
        var principal =
            role.Length == 0 ? PrincipalMother.Anonymous() : PrincipalMother.WithRoles(role);

        // Act
        var result = await _authorizationService.AuthorizeAsync(
            principal,
            AuthorizationPolicies.Impersonator
        );

        // Assert
        Assert.Equal(expected, result.Succeeded);
    }

    /// <summary>Verifies a caller holding both roles succeeds, and that <c>Support</c> alone does not satisfy the <c>Administrator</c> policy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AuthorizeAsync_SupportRole_DoesNotSatisfyTheAdministratorPolicy_Test()
    {
        // Arrange
        var both = PrincipalMother.WithRoles("Support", "Administrator");
        var support = PrincipalMother.WithRoles("Support");

        // Act
        var bothImpersonator = await _authorizationService.AuthorizeAsync(
            both,
            AuthorizationPolicies.Impersonator
        );
        var supportAdministrator = await _authorizationService.AuthorizeAsync(
            support,
            AuthorizationPolicies.Administrator
        );

        // Assert
        Assert.Multiple(
            () => Assert.True(bothImpersonator.Succeeded),
            () => Assert.False(supportAdministrator.Succeeded)
        );
    }
}

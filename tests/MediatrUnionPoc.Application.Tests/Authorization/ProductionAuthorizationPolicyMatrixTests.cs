using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>
/// Evaluates the policies exactly as <see cref="DependencyInjection.AddApplication"/> registers
/// them, through a real <see cref="IAuthorizationService"/>, so the documented rules (Update is
/// owner-only; Delete accepts the owner or an administrator) are enforced end to end rather than
/// asserted only through the <see cref="AuthorizationPolicies"/> XML docs.
/// </summary>
public sealed class ProductionAuthorizationPolicyMatrixTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "user-2";
    private const string AdministratorRole = "Administrator";
    private const string ViewerRole = "Viewer";

    private readonly ServiceProvider _provider;
    private readonly IAuthorizationService _authorizationService;
    private readonly OwnedProductResource _resource = new(OwnerId);

    /// <summary>Builds the real production registration and resolves its <see cref="IAuthorizationService"/>.</summary>
    public ProductionAuthorizationPolicyMatrixTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        _provider = services.BuildServiceProvider();
        _authorizationService = _provider.GetRequiredService<IAuthorizationService>();
    }

    /// <summary>
    /// Rows: every caller kind against the <c>ProductOwner</c> (Update, owner-only),
    /// <c>ProductOwnerOrAdministrator</c> (Delete, owner or administrator) and <c>Administrator</c>
    /// (role-only) policies.
    /// </summary>
    public static TheoryData<
        CallerKind,
        string,
        bool
    > AuthorizeAsync_CallerKindAndPolicy_DeterminesOutcome_Test_Data =>
        new()
        {
            { CallerKind.Owner, AuthorizationPolicies.ProductOwner, true },
            { CallerKind.NonOwnerAdministrator, AuthorizationPolicies.ProductOwner, false },
            { CallerKind.NonOwnerNonAdministrator, AuthorizationPolicies.ProductOwner, false },
            { CallerKind.NoClaims, AuthorizationPolicies.ProductOwner, false },
            { CallerKind.Owner, AuthorizationPolicies.ProductOwnerOrAdministrator, true },
            {
                CallerKind.NonOwnerAdministrator,
                AuthorizationPolicies.ProductOwnerOrAdministrator,
                true
            },
            {
                CallerKind.NonOwnerNonAdministrator,
                AuthorizationPolicies.ProductOwnerOrAdministrator,
                false
            },
            { CallerKind.NoClaims, AuthorizationPolicies.ProductOwnerOrAdministrator, false },
            { CallerKind.Owner, AuthorizationPolicies.Administrator, false },
            { CallerKind.NonOwnerAdministrator, AuthorizationPolicies.Administrator, true },
            { CallerKind.NonOwnerNonAdministrator, AuthorizationPolicies.Administrator, false },
            { CallerKind.NoClaims, AuthorizationPolicies.Administrator, false },
        };

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies each caller kind gets the documented outcome from each production policy.</summary>
    /// <param name="callerKind">The kind of caller to build.</param>
    /// <param name="policyName">The production policy to evaluate.</param>
    /// <param name="expectedSucceeded">Whether the policy is expected to succeed.</param>
    /// <returns>A task that completes when the assertion runs.</returns>
    // Auto Generated, verify expected behavior: owner-only Update, owner-or-administrator Delete, role-only Administrator.
    [Theory]
    [MemberData(nameof(AuthorizeAsync_CallerKindAndPolicy_DeterminesOutcome_Test_Data))]
    public async Task AuthorizeAsync_CallerKindAndPolicy_DeterminesOutcome_Test(
        CallerKind callerKind,
        string policyName,
        bool expectedSucceeded
    )
    {
        // Arrange
        var principal = BuildPrincipal(callerKind);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, _resource, policyName);

        // Assert
        Assert.Equal(expectedSucceeded, result.Succeeded);
    }

    /// <summary>Verifies an administrator who does not own the product cannot bypass the owner-only Update policy.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    // Auto Generated, verify expected behavior: administrators get no bypass on Update.
    [Fact]
    public async Task AuthorizeAsync_NonOwnerAdministratorUpdatingProduct_IsDenied_Test()
    {
        // Arrange
        var principal = BuildPrincipal(CallerKind.NonOwnerAdministrator);

        // Act
        var result = await _authorizationService.AuthorizeAsync(
            principal,
            _resource,
            AuthorizationPolicies.ProductOwner
        );

        // Assert
        Assert.False(result.Succeeded);
    }

    /// <summary>Verifies an administrator who does not own the product may delete it through the allowlisted Delete operation.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    // Auto Generated, verify expected behavior: administrators bypass ownership for Delete only.
    [Fact]
    public async Task AuthorizeAsync_NonOwnerAdministratorDeletingProduct_IsAllowed_Test()
    {
        // Arrange
        var principal = BuildPrincipal(CallerKind.NonOwnerAdministrator);

        // Act
        var result = await _authorizationService.AuthorizeAsync(
            principal,
            _resource,
            AuthorizationPolicies.ProductOwnerOrAdministrator
        );

        // Assert
        Assert.True(result.Succeeded);
    }

    /// <summary>Verifies the service handlers call denies a non-owner administrator on Update and allows them on Delete.</summary>
    /// <returns>A task that completes when the assertions run.</returns>
    // Auto Generated, verify expected behavior: ResourceAuthorizationService reflects the real policy set.
    [Fact]
    public async Task AuthorizeAsync_ResourceAuthorizationServiceWithProductionPolicies_ReflectsDocumentedRules_Test()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<ResourceAuthorizationService>();
        var administrator = BuildPrincipal(CallerKind.NonOwnerAdministrator);

        // Act
        var updateByAdministrator = await sut.AuthorizeAsync(
            administrator,
            _resource,
            AuthorizationPolicies.ProductOwner,
            TestContext.Current.CancellationToken
        );
        var deleteByAdministrator = await sut.AuthorizeAsync(
            administrator,
            _resource,
            AuthorizationPolicies.ProductOwnerOrAdministrator,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(updateByAdministrator);
        Assert.Null(deleteByAdministrator);
    }

    private static ClaimsPrincipal BuildPrincipal(CallerKind callerKind) =>
        callerKind switch
        {
            CallerKind.Owner => PrincipalMother.WithId(OwnerId),
            CallerKind.NonOwnerAdministrator => PrincipalWithIdAndRole(
                OtherUserId,
                AdministratorRole
            ),
            CallerKind.NonOwnerNonAdministrator => PrincipalWithIdAndRole(OtherUserId, ViewerRole),
            CallerKind.NoClaims => PrincipalMother.Anonymous(),
            _ => throw new ArgumentOutOfRangeException(nameof(callerKind), callerKind, null),
        };

    private static ClaimsPrincipal PrincipalWithIdAndRole(string id, string role)
    {
        var principal = PrincipalMother.WithId(id);
        principal.AddIdentity(PrincipalMother.WithRoles(role).Identities.First());
        return principal;
    }
}

using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using NSubstitute;
using static MediatrUnionPoc.Application.Tests.TestData.ImpersonationMother;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="IssueImpersonationTokenHandler"/> against a substituted
/// <see cref="IImpersonationTokenIssuer"/>: every outcome, the role-escalation rules, chained
/// impersonation. Recording each attempt is the audit behavior's job, tested with it.
/// </summary>
public sealed class IssueImpersonationTokenHandlerTests
{
    private const string Secret = "the-signed-token-value-that-must-never-be-logged";

    private readonly IImpersonationTokenIssuer _issuer =
        Substitute.For<IImpersonationTokenIssuer>();

    /// <summary>Wires the issuer substitute to echo the grant it is given back as a token.</summary>
    public IssueImpersonationTokenHandlerTests() =>
        _issuer
            .Issue(Arg.Any<ImpersonationGrant>())
            .Returns(call =>
            {
                var grant = call.Arg<ImpersonationGrant>();
                return new ImpersonationToken(
                    Secret,
                    DateTimeOffset.UnixEpoch.Add(grant.Lifetime),
                    grant.TargetUserId,
                    grant.Roles,
                    grant.ActorId
                );
            });

    /// <summary>Verifies an administrator asking for a plain identity gets a token, and the issuer is given the trimmed, approved grant with the default lifetime.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_AdministratorPlainIdentity_IssuesTokenWithDefaultLifetime_Test()
    {
        // Arrange
        var sut = Sut();

        // Act
        var result = await sut.Handle(
            Command(target: "  alice  ", reason: $"  {ValidReason}  ", ticket: " SUP-1 "),
            CancellationToken.None
        );

        // Assert
        var token = Assert.IsType<ImpersonationToken>(((IUnion)result).Value);
        _issuer
            .Received(1)
            .Issue(
                Arg.Is<ImpersonationGrant>(grant =>
                    grant.ActorId == AdminId
                    && grant.TargetUserId == "alice"
                    && grant.Reason == ValidReason
                    && grant.TicketReference == "SUP-1"
                    && grant.Roles.Count == 0
                    && grant.Lifetime == TimeSpan.FromMinutes(DefaultMinutes)
                )
            );
        Assert.Equal(Secret, token.Token);
    }

    /// <summary>Verifies a requested lifetime overrides the default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_RequestedLifetime_IsPassedToTheIssuer_Test()
    {
        // Act
        await Sut().Handle(Command(lifetime: 5), CancellationToken.None);

        // Assert
        _issuer
            .Received(1)
            .Issue(Arg.Is<ImpersonationGrant>(grant => grant.Lifetime == TimeSpan.FromMinutes(5)));
    }

    /// <summary>Verifies roles are trimmed and de-duplicated before being judged and granted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_RolesWithPaddingAndDuplicates_AreGrantedOnce_Test()
    {
        // Act
        await Sut()
            .Handle(
                Command(roles: [" Support ", "Support", "Administrator"]),
                CancellationToken.None
            );

        // Assert
        _issuer
            .Received(1)
            .Issue(
                Arg.Is<ImpersonationGrant>(grant =>
                    grant.Roles.SequenceEqual(new[] { "Support", "Administrator" })
                )
            );
    }

    /// <summary>Verifies a support user may grant a role they hold and may not grant one they lack, including Administrator.</summary>
    /// <param name="role">The requested role.</param>
    /// <param name="granted">Whether the token is issued.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Support", true)]
    [InlineData("Administrator", false)]
    [InlineData("Auditor", false)]
    public async Task Handle_SupportCaller_MayOnlyGrantRolesItHolds_Test(string role, bool granted)
    {
        // Arrange
        var sut = Sut(assignable: ["Support", "Administrator", "Auditor"]);
        var support = Caller(SupportId, [AuthorizationRoles.Support]);

        // Act
        var result = await sut.Handle(
            Command(principal: support, roles: [role]),
            CancellationToken.None
        );

        // Assert
        var value = ((IUnion)result).Value;
        if (granted)
        {
            Assert.IsType<ImpersonationToken>(value);
        }
        else
        {
            Assert.IsType<NotAuthorized>(value);
            _issuer.DidNotReceive().Issue(Arg.Any<ImpersonationGrant>());
        }
    }

    /// <summary>Verifies an administrator can grant any assignable role, but never one outside the assignable list.</summary>
    /// <param name="role">The requested role.</param>
    /// <param name="granted">Whether the token is issued.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Administrator", true)]
    [InlineData("Support", true)]
    [InlineData("SuperUser", false)]
    public async Task Handle_AdministratorCaller_MayGrantOnlyAssignableRoles_Test(
        string role,
        bool granted
    )
    {
        // Act
        var result = await Sut().Handle(Command(roles: [role]), CancellationToken.None);

        // Assert
        var value = ((IUnion)result).Value;
        Assert.Equal(granted, value is ImpersonationToken);
        if (!granted)
        {
            var refusal = Assert.IsType<NotAuthorized>(value);
            Assert.Contains(role, Assert.Single(refusal.Reasons), StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies a caller already using an impersonation token is refused, whether the token carries the marker, the actor claim, or both, even as an administrator.</summary>
    /// <param name="marker">Whether the marker claim is present.</param>
    /// <param name="actor">The actor claim's subject, if present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "someone")]
    [InlineData(true, "someone")]
    public async Task Handle_CallerAlreadyImpersonating_IsRefused_Test(bool marker, string? actor)
    {
        // Arrange
        var chained = Caller(
            "bob",
            [AuthorizationRoles.Administrator],
            marker: marker,
            actor: actor
        );

        // Act
        var result = await Sut().Handle(Command(principal: chained), CancellationToken.None);

        // Assert
        var refusal = Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        Assert.Contains("chained", Assert.Single(refusal.Reasons), StringComparison.Ordinal);
        _issuer.DidNotReceive().Issue(Arg.Any<ImpersonationGrant>());
    }

    /// <summary>Verifies a caller with no subject cannot be attributed and is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CallerWithoutSubject_IsRefused_Test()
    {
        // Act
        var result = await Sut()
            .Handle(
                Command(principal: Caller(null, [AuthorizationRoles.Administrator])),
                CancellationToken.None
            );

        // Assert
        Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        _issuer.DidNotReceive().Issue(Arg.Any<ImpersonationGrant>());
    }

    /// <summary>Verifies a switched-off feature answers the disabled error and issues nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_Disabled_ReturnsDisabledErrorAndIssuesNothing_Test()
    {
        // Act
        var result = await Sut(enabled: false).Handle(Command(), CancellationToken.None);

        // Assert
        var error = Assert.IsType<Error>(((IUnion)result).Value);
        Assert.Equal(ImpersonationErrors.DisabledCode, error.Code);
        _issuer.DidNotReceive().Issue(Arg.Any<ImpersonationGrant>());
    }

    /// <summary>Verifies the constructor rejects null dependencies.</summary>
    [Fact]
    public void Ctor_NullDependencies_ThrowArgumentNullException_Test()
    {
        // Act
        var issuer = Assert.Throws<ArgumentNullException>(() =>
            new IssueImpersonationTokenHandler(null!, Settings())
        );
        var settings = Assert.Throws<ArgumentNullException>(() =>
            new IssueImpersonationTokenHandler(_issuer, null!)
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal("issuer", issuer.ParamName),
            () => Assert.Equal("settings", settings.ParamName)
        );
    }

    private IssueImpersonationTokenHandler Sut(bool enabled = true, string[]? assignable = null) =>
        new(_issuer, Settings(enabled, assignable ?? []));
}

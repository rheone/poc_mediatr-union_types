using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Tests.TestData;
using static MediatrUnionPoc.Application.Tests.TestData.ImpersonationMother;

namespace MediatrUnionPoc.Application.Tests.Auditing;

/// <summary>Tests <see cref="AuditIdentity"/> (who an audited request is from) and <see cref="AuditText"/> (the bound on free text).</summary>
public sealed class AuditIdentityTests
{
    /// <summary>Verifies no principal yields an unknown identity.</summary>
    [Fact]
    public void From_NullPrincipal_IsUnknown_Test()
    {
        // Act
        var identity = AuditIdentity.From(null);

        // Assert
        Assert.Same(AuditIdentity.Unknown, identity);
    }

    /// <summary>Verifies an ordinary caller is its own actor and effective identity, with no token id even if it carries a <c>jti</c>.</summary>
    [Fact]
    public void From_PlainCaller_ActorEqualsEffective_Test()
    {
        // Act
        var identity = AuditIdentity.From(Caller("alice", tokenId: "ordinary-token"));

        // Assert
        Assert.Equal(new AuditIdentity("alice", "alice", false, null), identity);
    }

    /// <summary>Verifies an impersonated principal is the <c>act</c> subject acting as the effective identity, carrying the token id.</summary>
    [Fact]
    public void From_ImpersonatedCaller_ActorIsTheActSubject_Test()
    {
        // Act
        var identity = AuditIdentity.From(Caller("alice", actor: "root", tokenId: "jti-7"));

        // Assert
        Assert.Equal(new AuditIdentity("root", "alice", true, "jti-7"), identity);
    }

    /// <summary>Verifies an impersonation marker without a readable actor leaves the actor unknown rather than guessing the effective identity.</summary>
    [Fact]
    public void From_MarkerWithoutActor_ActorIsUnknown_Test()
    {
        // Act
        var identity = AuditIdentity.From(Caller("alice", marker: true));

        // Assert
        Assert.Equal(new AuditIdentity(null, "alice", true, null), identity);
    }

    /// <summary>Verifies a principal with no id yields no ids.</summary>
    [Fact]
    public void From_CallerWithoutId_HasNoIds_Test()
    {
        // Act
        var identity = AuditIdentity.From(new ClaimsPrincipal(new ClaimsIdentity()));

        // Assert
        Assert.Equal(AuditIdentity.Unknown, identity);
    }

    /// <summary>Verifies text at or under the bound is returned as is, over it is cut to exactly the bound, and null stays null.</summary>
    [Fact]
    public void Limit_TextAroundTheBound_IsCutOnlyWhenTooLong_Test()
    {
        // Arrange
        var exact = new string('a', AuditText.MaxLength);
        var over = new string('a', AuditText.MaxLength + 1);

        // Act / Assert
        Assert.Multiple(
            () => Assert.Same(exact, AuditText.Limit(exact)),
            () => Assert.Equal(AuditText.MaxLength, AuditText.Limit(over).Length),
            () => Assert.EndsWith("...", AuditText.Limit(over), StringComparison.Ordinal),
            () => Assert.Null(AuditText.Limit(null))
        );
    }
}

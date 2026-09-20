using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Tests.TestData;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>Verifies the readers that answer whether a caller is impersonated, and who is really behind it, from a principal.</summary>
public class ImpersonationClaimsTests
{
    /// <summary>Verifies a principal is impersonated with the marker, with the actor claim, or with both, and an ordinary one is not.</summary>
    /// <param name="marker">Whether the marker claim is present.</param>
    /// <param name="actor">The actor subject, if present.</param>
    /// <param name="expected">Whether the principal counts as impersonated.</param>
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    [InlineData(false, "root", true)]
    [InlineData(true, "root", true)]
    public void IsImpersonated_MarkerOrActor_DeterminesResult_Test(
        bool marker,
        string? actor,
        bool expected
    )
    {
        // Arrange
        var principal = ImpersonationMother.Caller("alice", marker: marker, actor: actor);

        // Act
        var result = principal.IsImpersonated();

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Verifies a marker claim whose value is not <c>true</c> does not count.</summary>
    [Fact]
    public void IsImpersonated_MarkerWithOtherValue_IsFalse_Test()
    {
        // Arrange
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ImpersonationClaims.Impersonated, "false")], "Test")
        );

        // Act / Assert
        Assert.False(principal.IsImpersonated());
    }

    /// <summary>Verifies the actor id is the <c>sub</c> of the <c>act</c> JSON object.</summary>
    [Fact]
    public void GetActorId_ActClaim_ReturnsTheActorSubject_Test()
    {
        // Arrange
        var principal = ImpersonationMother.Caller("alice", actor: "root");

        // Act / Assert
        Assert.Equal("root", principal.GetActorId());
    }

    /// <summary>Verifies an absent, empty, malformed or wrongly shaped actor claim reads as no actor rather than throwing.</summary>
    /// <param name="value">The <c>act</c> claim value, or <see langword="null"/> for no claim.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[\"root\"]")]
    [InlineData("{\"name\":\"root\"}")]
    [InlineData("{\"sub\":42}")]
    public void GetActorId_MissingOrMalformedActor_ReturnsNull_Test(string? value)
    {
        // Arrange
        List<Claim> claims = value is null ? [] : [new Claim(ImpersonationClaims.Actor, value)];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act / Assert
        Assert.Null(principal.GetActorId());
    }

    /// <summary>Verifies both readers reject a null principal.</summary>
    [Fact]
    public void Readers_NullPrincipal_ThrowArgumentNullException_Test()
    {
        // Act / Assert
        Assert.Multiple(
            () =>
                Assert.Throws<ArgumentNullException>(() =>
                    ((ClaimsPrincipal)null!).IsImpersonated()
                ),
            () => Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).GetActorId())
        );
    }
}

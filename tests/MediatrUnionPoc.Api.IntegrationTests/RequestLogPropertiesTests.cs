using System.Security.Claims;
using MediatrUnionPoc.Api.Logging;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises <see cref="RequestLogProperties"/> on hand-built principals, without a host.</summary>
public sealed class RequestLogPropertiesTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    /// <summary>Verifies an unauthenticated principal yields no properties, so no identity is invented.</summary>
    [Fact]
    public void Describe_Anonymous_YieldsNothing_Test()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var properties = RequestLogProperties.Describe(principal);

        // Assert
        Assert.Empty(properties);
    }

    /// <summary>Verifies an ordinary caller yields its id and a false impersonation flag, with no actor.</summary>
    [Fact]
    public void Describe_OrdinaryCaller_YieldsUserIdAndFalseFlag_Test()
    {
        // Arrange
        var principal = Principal(new Claim(ClaimTypes.NameIdentifier, "alice"));

        // Act
        var properties = RequestLogProperties
            .Describe(principal)
            .ToDictionary(p => p.Key, p => p.Value);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("alice", properties[RequestLogProperties.UserId]),
            () => Assert.False((bool)properties[RequestLogProperties.IsImpersonated]!),
            () => Assert.DoesNotContain(RequestLogProperties.ImpersonatedBy, properties.Keys)
        );
    }

    /// <summary>Verifies an impersonated caller yields the target, the flag and the actor from the <c>act</c> claim.</summary>
    [Fact]
    public void Describe_ImpersonatedCaller_YieldsTargetFlagAndActor_Test()
    {
        // Arrange
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, "alice"),
            new Claim(ImpersonationClaims.Impersonated, "true"),
            new Claim(ImpersonationClaims.Actor, """{"sub":"root"}"""),
            new Claim(ImpersonationClaims.Reason, "a secret-ish reason")
        );

        // Act
        var properties = RequestLogProperties
            .Describe(principal)
            .ToDictionary(p => p.Key, p => p.Value);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("alice", properties[RequestLogProperties.UserId]),
            () => Assert.True((bool)properties[RequestLogProperties.IsImpersonated]!),
            () => Assert.Equal("root", properties[RequestLogProperties.ImpersonatedBy]),
            () => Assert.Equal(3, properties.Count)
        );
    }
}

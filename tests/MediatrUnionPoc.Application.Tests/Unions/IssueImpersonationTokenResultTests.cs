using System.Runtime.CompilerServices;
using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using MediatrUnionPoc.Application.Tests.TestData;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>Verifies <see cref="IssueImpersonationTokenResult"/>'s factories, and that the token case and the command keep the credential and the policy in their proper places.</summary>
public class IssueImpersonationTokenResultTests
{
    private const string Secret = "signed-token-value";

    /// <summary>Verifies the validation factory yields the union's <see cref="ValidationErrors"/> case.</summary>
    [Fact]
    public void FromValidationErrors_Errors_ReturnsTheValidationErrorsCase_Test()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Reason", "required")]);

        // Act
        var result = IssueImpersonationTokenResult.FromValidationErrors(errors);

        // Assert
        Assert.Equal(errors, ((IUnion)result).Value);
    }

    /// <summary>Verifies the authorization factory yields the union's <see cref="NotAuthorized"/> case.</summary>
    [Fact]
    public void FromNotAuthorized_Failure_ReturnsTheNotAuthorizedCase_Test()
    {
        // Arrange
        var failure = new NotAuthorized(["no"]);

        // Act
        var result = IssueImpersonationTokenResult.FromNotAuthorized(failure);

        // Assert
        Assert.Equal(failure, ((IUnion)result).Value);
    }

    /// <summary>Verifies both factories reject null.</summary>
    [Fact]
    public void Factories_Null_ThrowArgumentNullException_Test()
    {
        // Act / Assert
        Assert.Multiple(
            () =>
                Assert.Throws<ArgumentNullException>(() =>
                    IssueImpersonationTokenResult.FromValidationErrors(null!)
                ),
            () =>
                Assert.Throws<ArgumentNullException>(() =>
                    IssueImpersonationTokenResult.FromNotAuthorized(null!)
                )
        );
    }

    /// <summary>Verifies the token case's <c>ToString</c> never reveals the credential but does name the identities.</summary>
    [Fact]
    public void ToString_TokenCase_OmitsTheCredential_Test()
    {
        // Arrange
        var token = new ImpersonationToken(
            Secret,
            DateTimeOffset.UnixEpoch,
            "alice",
            ["Support"],
            "root"
        );

        // Act
        var text = token.ToString();

        // Assert
        Assert.Multiple(
            () => Assert.DoesNotContain(Secret, text, StringComparison.Ordinal),
            () => Assert.Contains("alice", text, StringComparison.Ordinal),
            () => Assert.Contains("root", text, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies the command is gated by the <c>Impersonator</c> policy and rejects a null principal.</summary>
    [Fact]
    public void Command_PolicyNameAndPrincipal_AreGuarded_Test()
    {
        // Act
        var command = ImpersonationMother.Command();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(AuthorizationPolicies.Impersonator, command.PolicyName),
            () =>
                Assert.Throws<ArgumentNullException>(() =>
                    new IssueImpersonationTokenCommand(
                        "a",
                        null,
                        "r",
                        null,
                        null,
                        (ClaimsPrincipal)null!
                    )
                )
        );
    }
}

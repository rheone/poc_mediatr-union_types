using System.Security.Claims;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>Object mother for the <see cref="ClaimsPrincipal"/> callers several test classes simulate.</summary>
public static class PrincipalMother
{
    /// <summary>The authentication type given to every principal built here, so <c>IsAuthenticated</c> is true.</summary>
    private const string AuthenticationType = "Test";

    /// <summary>Builds a caller claiming exactly the given roles.</summary>
    /// <param name="roles">The <see cref="ClaimTypes.Role"/> values to claim; none yields a caller with no roles.</param>
    /// <returns>A new authenticated principal.</returns>
    public static ClaimsPrincipal WithRoles(params string[] roles) =>
        new(
            new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                AuthenticationType
            )
        );

    /// <summary>Builds a caller whose <see cref="ClaimTypes.NameIdentifier"/> is <paramref name="id"/>.</summary>
    /// <param name="id">The caller identifier to claim.</param>
    /// <returns>A new authenticated principal.</returns>
    public static ClaimsPrincipal WithId(string id) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], AuthenticationType));

    /// <summary>Builds a caller with no claims at all.</summary>
    /// <returns>A new unauthenticated principal.</returns>
    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}

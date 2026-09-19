namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>The kinds of caller the production authorization policy matrix is exercised against.</summary>
public enum CallerKind
{
    /// <summary>Owns the resource and holds no role.</summary>
    Owner,

    /// <summary>Holds the <c>Administrator</c> role but does not own the resource.</summary>
    NonOwnerAdministrator,

    /// <summary>Neither owns the resource nor holds the <c>Administrator</c> role.</summary>
    NonOwnerNonAdministrator,

    /// <summary>Carries no claims at all.</summary>
    NoClaims,
}

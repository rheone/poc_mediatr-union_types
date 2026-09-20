using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>The operation requirements resource-based policies are registered against, so operation names live in one place.</summary>
public static class AuthorizationOperations
{
    /// <summary>Requirement for updating an existing resource.</summary>
    public static readonly OperationAuthorizationRequirement Update = new()
    {
        Name = nameof(Update),
    };
}

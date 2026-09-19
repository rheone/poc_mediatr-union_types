using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Application.Tests.Authorization;

/// <summary>Verifies <see cref="AdministratorRequirement"/> guards its constructor argument.</summary>
public class AdministratorRequirementTests
{
    /// <summary>Verifies the constructor rejects a null role array instead of storing it and failing later in the handler.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullAllowedRoles_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new AdministratorRequirement(null!));

        // Assert
        Assert.Equal("allowedRoles", ex.ParamName);
    }
}

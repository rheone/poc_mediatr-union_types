using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests;

/// <summary>Verifies <see cref="DependencyInjection.AddApplication"/> guards its argument.</summary>
public class DependencyInjectionTests
{
    /// <summary>Verifies registration rejects a null service collection.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void AddApplication_NullServices_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            DependencyInjection.AddApplication(null!)
        );

        // Assert
        Assert.Equal("services", ex.ParamName);
    }
}

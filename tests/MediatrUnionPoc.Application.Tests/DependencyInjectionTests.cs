using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests;

/// <summary>Verifies <see cref="DependencyInjection.AddApplication"/> guards its argument and supplies a clock.</summary>
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

    /// <summary>Verifies the system clock is registered by default, so handlers that stamp times resolve out of the box.</summary>
    [Fact]
    public void AddApplication_NoClockRegistered_RegistersSystemTimeProvider_Test()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        using var provider = services.AddApplication().BuildServiceProvider();

        // Assert
        Assert.Same(TimeProvider.System, provider.GetService<TimeProvider>());
    }

    /// <summary>Verifies a clock registered beforehand (a test double, say) is kept rather than replaced.</summary>
    [Fact]
    public void AddApplication_ClockAlreadyRegistered_KeepsIt_Test()
    {
        // Arrange
        var custom = new TestData.FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var services = new ServiceCollection().AddSingleton<TimeProvider>(custom);

        // Act
        using var provider = services.AddApplication().BuildServiceProvider();

        // Assert
        Assert.Same(custom, provider.GetService<TimeProvider>());
    }
}

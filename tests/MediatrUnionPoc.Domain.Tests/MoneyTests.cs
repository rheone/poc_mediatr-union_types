namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="Money"/>'s Vogen-generated <c>Validate</c> rejects negative amounts from
/// every factory, and accepts everything else.
/// </summary>
public class MoneyTests
{
    /// <summary>Verifies a negative amount fails validation via <c>Money.TryFrom</c>.</summary>
    [Fact]
    public void TryFrom_NegativeAmount_ReturnsFalse_Test()
    {
        // Arrange
        const decimal negativeAmount = -0.01m;

        // Act
        var succeeded = Money.TryFrom(negativeAmount, out _);

        // Assert
        Assert.False(succeeded);
    }

    /// <summary>Verifies zero is a valid amount.</summary>
    [Fact]
    public void From_Zero_ReturnsZeroValue_Test()
    {
        // Arrange
        const decimal zero = 0m;

        // Act
        var money = Money.From(zero);

        // Assert
        Assert.Equal(zero, money.Value);
    }

    /// <summary>Verifies a positive amount round-trips through <see cref="Money.From"/> unchanged.</summary>
    [Fact]
    public void From_PositiveAmount_RoundTripsUnchanged_Test()
    {
        // Arrange
        const decimal amount = 9.99m;

        // Act
        var money = Money.From(amount);

        // Assert
        Assert.Equal(amount, money.Value);
    }

    /// <summary>Verifies <see cref="Money.From"/> rejects a negative amount by throwing, mirroring <c>TryFrom</c> returning false.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void From_NegativeAmount_ThrowsValueObjectValidationException_Test()
    {
        // Arrange
        const decimal negativeAmount = -0.01m;

        // Act
        var ex = Assert.Throws<Vogen.ValueObjectValidationException>(() =>
            Money.From(negativeAmount)
        );

        // Assert
        Assert.Contains("negative", ex.Message);
    }

    /// <summary>Verifies <c>TryFrom</c> succeeds and yields the value for a valid amount.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void TryFrom_ValidAmount_ReturnsTrueWithValue_Test()
    {
        // Arrange
        const decimal amount = 5m;

        // Act
        var succeeded = Money.TryFrom(amount, out var money);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(amount, money.Value);
    }
}

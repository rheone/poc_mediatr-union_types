namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="Money"/>'s Vogen-generated <c>Validate</c> rejects negative amounts from
/// every factory, and accepts everything else.
/// </summary>
public class MoneyTests
{
    /// <summary>Verifies a negative amount fails validation via <c>Money.TryFrom</c>.</summary>
    [Fact]
    public void TryFrom_negative_amount_returns_false()
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
    public void From_zero_succeeds_with_zero_value()
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
    public void From_positive_amount_round_trips_unchanged()
    {
        // Arrange
        const decimal amount = 9.99m;

        // Act
        var money = Money.From(amount);

        // Assert
        Assert.Equal(amount, money.Value);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="Money.From"/> rejects a negative amount by throwing, mirroring <c>TryFrom</c> returning false.</summary>
    [Fact]
    public void From_negative_amount_throws_validation_exception()
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

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <c>TryFrom</c> succeeds and yields the value for a valid amount.</summary>
    [Fact]
    public void TryFrom_valid_amount_returns_true_with_value()
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

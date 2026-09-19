namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="Money"/>'s Vogen-generated <c>Validate</c> rejects negative amounts from
/// every factory, and accepts everything else.
/// </summary>
public class MoneyTests
{
    /// <summary>Verifies a negative amount fails validation via <c>Money.TryFrom</c>.</summary>
    [Fact]
    public void Negative_amounts_fail_validation()
    {
        var succeeded = Money.TryFrom(-0.01m, out _);

        Assert.False(succeeded);
    }

    /// <summary>Verifies zero is a valid amount.</summary>
    [Fact]
    public void Zero_is_valid()
    {
        var money = Money.From(0m);

        Assert.Equal(0m, money.Value);
    }

    /// <summary>Verifies a positive amount round-trips through <see cref="Money.From"/> unchanged.</summary>
    [Fact]
    public void Positive_amounts_round_trip_unchanged()
    {
        var money = Money.From(9.99m);

        Assert.Equal(9.99m, money.Value);
    }
}

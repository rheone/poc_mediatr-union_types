namespace MediatrUnionPoc.Domain.Tests;

/// <summary>Verifies <see cref="ProductNames"/>: the one rule for when two product names are duplicates.</summary>
public class ProductNamesTests
{
    /// <summary>Verifies names differing only by case and surrounding whitespace share one key.</summary>
    [Fact]
    public void Normalize_DifferentCaseAndPadding_YieldsSameKey_Test()
    {
        // Arrange
        const string padded = "  wIdGeT \t";

        // Act
        var key = ProductNames.Normalize(padded);

        // Assert
        Assert.Equal(ProductNames.Normalize("Widget"), key);
    }
}

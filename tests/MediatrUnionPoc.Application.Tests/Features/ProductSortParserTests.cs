using MediatrUnionPoc.Application.Features.Products.GetPaged;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="ProductSortParser"/> turns the <c>sort</c> text into allowlisted keys, and rejects everything else with a message per problem.</summary>
public class ProductSortParserTests
{
    /// <summary>Verifies null, empty and blank text all mean "no sort requested" and count as valid.</summary>
    /// <param name="text">The sort text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_NoSortRequested_SucceedsWithNoKeys_Test(string? text)
    {
        // Act
        var ok = ProductSortParser.TryParse(text, out var sort, out var errors);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () => Assert.Empty(sort),
            () => Assert.Empty(errors)
        );
    }

    /// <summary>Verifies keys keep their order, a leading minus means descending, and case and surrounding whitespace do not matter.</summary>
    /// <param name="text">The sort text.</param>
    [Theory]
    [InlineData("name,-price")]
    [InlineData("NAME,-Price")]
    [InlineData("  name , -price  ")]
    public void TryParse_NameThenDescendingPrice_ReturnsKeysInOrder_Test(string text)
    {
        // Act
        var ok = ProductSortParser.TryParse(text, out var sort, out _);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () =>
                Assert.Equal(
                    [
                        new ProductSort(ProductSortField.Name, SortDirection.Ascending),
                        new ProductSort(ProductSortField.Price, SortDirection.Descending),
                    ],
                    sort
                )
        );
    }

    /// <summary>Verifies the camel-case <c>createdAt</c> field is recognised, ascending and descending.</summary>
    [Fact]
    public void TryParse_CreatedAtBothDirections_IsRecognised_Test()
    {
        // Act
        var okDescending = ProductSortParser.TryParse("-createdAt", out var descending, out _);
        var okAscending = ProductSortParser.TryParse("createdAt", out var ascending, out _);

        // Assert
        Assert.Multiple(
            () => Assert.True(okDescending && okAscending),
            () =>
                Assert.Equal(
                    [new ProductSort(ProductSortField.CreatedAt, SortDirection.Descending)],
                    descending
                ),
            () =>
                Assert.Equal(
                    [new ProductSort(ProductSortField.CreatedAt, SortDirection.Ascending)],
                    ascending
                )
        );
    }

    /// <summary>Verifies anything outside the allowlist fails, including numeric strings that an enum parser would otherwise accept, and the message names the offender and the allowed fields.</summary>
    /// <param name="text">The sort text.</param>
    /// <param name="offender">The token expected to be named in the error.</param>
    [Theory]
    [InlineData("colour", "colour")]
    [InlineData("name,-weight", "weight")]
    [InlineData("1", "1")]
    [InlineData("-", "-")]
    public void TryParse_FieldOutsideAllowlist_FailsNamingTheFieldAndTheAllowedOnes_Test(
        string text,
        string offender
    )
    {
        // Act
        var ok = ProductSortParser.TryParse(text, out var sort, out var errors);

        // Assert
        Assert.Multiple(
            () => Assert.False(ok),
            () => Assert.Empty(sort),
            () => Assert.Single(errors),
            () => Assert.Contains($"'{offender}'", errors[0]),
            () => Assert.Contains("name, price, createdAt", errors[0])
        );
    }

    /// <summary>Verifies naming the same field twice (in either direction) is rejected rather than silently keeping one.</summary>
    /// <param name="text">The sort text.</param>
    [Theory]
    [InlineData("name,name")]
    [InlineData("price,-price")]
    public void TryParse_SameFieldTwice_Fails_Test(string text)
    {
        // Act
        var ok = ProductSortParser.TryParse(text, out _, out var errors);

        // Assert
        Assert.Multiple(() => Assert.False(ok), () => Assert.Single(errors));
    }

    /// <summary>Verifies an empty key between commas is rejected.</summary>
    [Fact]
    public void TryParse_EmptyKey_Fails_Test()
    {
        // Act
        var ok = ProductSortParser.TryParse("name,,price", out _, out var errors);

        // Assert
        Assert.Multiple(() => Assert.False(ok), () => Assert.Single(errors));
    }

    /// <summary>Verifies every problem is reported, not just the first.</summary>
    [Fact]
    public void TryParse_SeveralProblems_ReportsEachOne_Test()
    {
        // Act
        var ok = ProductSortParser.TryParse("bogus,name,-name,other", out _, out var errors);

        // Assert
        Assert.Multiple(() => Assert.False(ok), () => Assert.Equal(3, errors.Count));
    }
}

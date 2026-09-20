using System.Text.Json;

namespace MediatrUnionPoc.Domain.Tests;

/// <summary>Verifies the sort vocabulary: the default order and the wire names of the allowlisted fields.</summary>
public class ProductSortTests
{
    /// <summary>Verifies the default order is name ascending and nothing else.</summary>
    [Fact]
    public void Default_IsNameAscendingOnly_Test()
    {
        // Act
        var sort = ProductSort.Default;

        // Assert
        Assert.Equal([new ProductSort(ProductSortField.Name, SortDirection.Ascending)], sort);
    }

    /// <summary>Verifies a sort key serialises with the camel-case field name and lower-case direction the HTTP contract uses.</summary>
    [Fact]
    public void Serialize_CreatedAtDescending_UsesContractNames_Test()
    {
        // Arrange
        var sort = new ProductSort(ProductSortField.CreatedAt, SortDirection.Descending);

        // Act
        var json = JsonSerializer.Serialize(
            sort,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );

        // Assert
        Assert.Equal("""{"field":"createdAt","direction":"descending"}""", json);
    }
}

namespace MediatrUnionPoc.Domain.Tests;

/// <summary>Verifies <see cref="ProductVersion"/>: where it starts, how it advances, and its weak-ETag wire form.</summary>
public class ProductVersionTests
{
    /// <summary>Verifies a new product's version starts at 1.</summary>
    [Fact]
    public void Initial_Called_IsOne_Test()
    {
        // Arrange

        // Act
        var version = ProductVersion.Initial;

        // Assert
        Assert.Equal(1L, version.Value);
    }

    /// <summary>Verifies <see cref="ProductVersion.Next"/> advances by exactly one.</summary>
    [Fact]
    public void Next_FromFive_ReturnsSix_Test()
    {
        // Arrange
        var version = ProductVersion.From(5);

        // Act
        var next = version.Next();

        // Assert
        Assert.Equal(6L, next.Value);
    }

    /// <summary>Verifies <see cref="ProductVersion.ToETag"/> renders the weak ETag wire form.</summary>
    [Fact]
    public void ToETag_VersionSeven_ReturnsWeakQuotedForm_Test()
    {
        // Arrange
        var version = ProductVersion.From(7);

        // Act
        var etag = version.ToETag();

        // Assert
        Assert.Equal("W/\"7\"", etag);
    }

    /// <summary>Verifies a weak ETag produced by <see cref="ProductVersion.ToETag"/> parses back to the same version.</summary>
    [Fact]
    public void ParseETag_WeakETagOfVersionSeven_ReturnsVersionSeven_Test()
    {
        // Arrange
        const string etag = "W/\"7\"";

        // Act
        var parsed = ProductVersion.ParseETag(etag);

        // Assert
        Assert.Equal(7L, parsed?.Value);
    }

    /// <summary>Verifies anything that is not a weak-quoted positive integer is rejected rather than parsed.</summary>
    /// <param name="malformed">The malformed header value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("\"7\"")]
    [InlineData("W/7")]
    [InlineData("W/\"7")]
    [InlineData("W/\"abc\"")]
    [InlineData("W/\"0\"")]
    [InlineData("W/\"-3\"")]
    [InlineData("W/\"\"")]
    [InlineData("*")]
    public void ParseETag_Malformed_ReturnsNull_Test(string? malformed)
    {
        // Arrange

        // Act
        var parsed = ProductVersion.ParseETag(malformed);

        // Assert
        Assert.Null(parsed);
    }
}

using MediatrUnionPoc.Application.Common;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="Optional{T}"/> distinguishes "not supplied" from "supplied, whatever the value".</summary>
public class OptionalTests
{
    /// <summary>Verifies a default-initialised optional (what a missing JSON member deserialises to) is absent.</summary>
    [Fact]
    public void Default_IsAbsent_Test()
    {
        // Act
        Optional<string?> optional = default;

        // Assert
        Assert.False(optional.IsPresent);
    }

    /// <summary>Verifies an optional built from a value is present and yields that value.</summary>
    [Fact]
    public void Of_Value_IsPresentAndYieldsValue_Test()
    {
        // Act
        var optional = Optional<decimal?>.Of(9.99m);

        // Assert
        Assert.Multiple(
            () => Assert.True(optional.IsPresent),
            () => Assert.Equal(9.99m, optional.Value)
        );
    }

    /// <summary>Verifies an explicit null is a supplied value, distinct from absence.</summary>
    [Fact]
    public void Of_Null_IsPresentWithNullValue_Test()
    {
        // Act
        var optional = Optional<string?>.Of(null);

        // Assert
        Assert.Multiple(() => Assert.True(optional.IsPresent), () => Assert.Null(optional.Value));
    }

    /// <summary>Verifies reading the value of an absent optional fails loudly instead of returning a default that looks like data.</summary>
    [Fact]
    public void Value_Absent_ThrowsInvalidOperationException_Test()
    {
        // Arrange
        Optional<decimal?> optional = default;

        // Act
        var ex = Record.Exception(() => optional.Value);

        // Assert
        Assert.IsType<InvalidOperationException>(ex);
    }

    /// <summary>Verifies <see cref="Optional{T}.GetValueOrDefault"/> collapses absence to the default and passes a present value through.</summary>
    [Fact]
    public void GetValueOrDefault_AbsentAndPresent_ReturnsDefaultAndValue_Test()
    {
        // Arrange
        Optional<decimal?> absent = default;
        var present = Optional<decimal?>.Of(9.99m);

        // Act
        var results = (absent.GetValueOrDefault(), present.GetValueOrDefault());

        // Assert
        Assert.Equal(((decimal?)null, (decimal?)9.99m), results);
    }
}

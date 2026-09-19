using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies the case types that carry a collection compare by value, not by reference. Default
/// record equality compares an <c>IReadOnlyCollection&lt;T&gt;</c> field via
/// <c>EqualityComparer&lt;IReadOnlyCollection&lt;T&gt;&gt;.Default</c>, which falls back to
/// reference equality since the collection type itself doesn't override <c>Equals</c> — so two
/// instances built from equal-looking collections were never equal unless they shared the exact
/// same array instance underneath.
/// </summary>
public class CaseTypeEqualityTests
{
    private const string InsufficientStock = "insufficient stock";
    private const string ProductDiscontinued = "product discontinued";
    private const string MissingAdminRole = "missing admin role";

    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    );

    /// <summary>Verifies two <see cref="Failure"/> instances built from equal reasons compare equal.</summary>
    [Fact]
    public void Equals_FailureWithSameReasons_ReturnsTrueAndEqualHashCodes_Test()
    {
        // Arrange
        var first = new Failure([InsufficientStock, ProductDiscontinued]);
        var second = new Failure([InsufficientStock, ProductDiscontinued]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.Multiple(
            () => Assert.True(equal),
            () => Assert.Equal(first.GetHashCode(), second.GetHashCode())
        );
    }

    /// <summary>
    /// Verifies <see cref="Failure"/> equality doesn't depend on reason order, and that reordered
    /// instances still satisfy the Equals/GetHashCode contract (equal objects must hash equal) —
    /// <see cref="Equals_FailureWithSameReasons_ReturnsTrueAndEqualHashCodes_Test"/> only proves that for identically-ordered
    /// reasons, which alone can't catch a hash implementation that is order-sensitive, unlike the
    /// set equality it must agree with.
    /// </summary>
    [Fact]
    public void Equals_FailureWithReorderedReasons_ReturnsTrueAndEqualHashCodes_Test()
    {
        // Arrange
        var first = new Failure(["a", "b"]);
        var second = new Failure(["b", "a"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.Multiple(
            () => Assert.True(equal),
            () => Assert.Equal(first.GetHashCode(), second.GetHashCode())
        );
    }

    /// <summary>Verifies <see cref="Failure"/> instances with different reasons are not equal.</summary>
    [Fact]
    public void Equals_FailureWithDifferentReasons_ReturnsFalse_Test()
    {
        // Arrange
        var first = new Failure([InsufficientStock]);
        var second = new Failure([ProductDiscontinued]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    /// <summary>Verifies duplicate reasons are collapsed on construction, so a duplicated reason compares equal to a single one.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_FailureWithDuplicateReasons_CollapsesToSingleReason_Test()
    {
        // Arrange
        var duplicated = new Failure(["a", "a"]);
        var single = new Failure(["a"]);

        // Act
        var equal = duplicated.Equals(single);

        // Assert
        Assert.Multiple(() => Assert.True(equal), () => Assert.Single(duplicated.Reasons));
    }

    /// <summary>Verifies a <see cref="Failure"/> built from a <see langword="null"/> reasons sequence has no reasons rather than throwing.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_FailureWithNullReasons_YieldsEmptyReasons_Test()
    {
        // Arrange
        IEnumerable<string>? reasons = null;

        // Act
        var failure = new Failure(reasons);

        // Assert
        Assert.Empty(failure.Reasons);
    }

    /// <summary>Verifies two <see cref="NotAuthorized"/> instances built from equal reasons compare equal.</summary>
    [Fact]
    public void Equals_NotAuthorizedWithSameReasons_ReturnsTrueAndEqualHashCodes_Test()
    {
        // Arrange
        var first = new NotAuthorized([MissingAdminRole]);
        var second = new NotAuthorized([MissingAdminRole]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.Multiple(
            () => Assert.True(equal),
            () => Assert.Equal(first.GetHashCode(), second.GetHashCode())
        );
    }

    /// <summary>Verifies <see cref="NotAuthorized"/> instances with different reasons are not equal.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_NotAuthorizedWithDifferentReasons_ReturnsFalse_Test()
    {
        // Arrange
        var first = new NotAuthorized([MissingAdminRole]);
        var second = new NotAuthorized(["not the owner"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    /// <summary>Verifies a <see cref="NotAuthorized"/> built from a <see langword="null"/> reasons sequence has no reasons rather than throwing.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NotAuthorizedWithNullReasons_YieldsEmptyReasons_Test()
    {
        // Arrange
        IEnumerable<string>? reasons = null;

        // Act
        var notAuthorized = new NotAuthorized(reasons);

        // Assert
        Assert.Empty(notAuthorized.Reasons);
    }

    /// <summary>Verifies two <see cref="ValidationErrors"/> instances built from equal field failures compare equal.</summary>
    [Fact]
    public void Equals_ValidationErrorsWithSameErrors_ReturnsTrueAndEqualHashCodes_Test()
    {
        // Arrange
        var first = new ValidationErrors([new ValidationError("Name", "required")]);
        var second = new ValidationErrors([new ValidationError("Name", "required")]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.Multiple(
            () => Assert.True(equal),
            () => Assert.Equal(first.GetHashCode(), second.GetHashCode())
        );
    }

    /// <summary>Verifies <see cref="ValidationErrors"/> instances with different field failures are not equal.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_ValidationErrorsWithDifferentErrors_ReturnsFalse_Test()
    {
        // Arrange
        var first = new ValidationErrors([new ValidationError("Name", "required")]);
        var second = new ValidationErrors([new ValidationError("Price", "required")]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    /// <summary>Verifies a <see cref="ValidationErrors"/> built from a <see langword="null"/> sequence has no errors rather than throwing.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_ValidationErrorsWithNullErrors_YieldsEmptyErrors_Test()
    {
        // Arrange
        IEnumerable<ValidationError>? errors = null;

        // Act
        var validationErrors = new ValidationErrors(errors);

        // Assert
        Assert.Empty(validationErrors.Errors);
    }

    /// <summary>Verifies <see cref="Failure"/> never equals <see langword="null"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_FailureWithNull_ReturnsFalse_Test()
    {
        // Arrange
        var failure = new Failure([InsufficientStock]);

        // Act
        var equal = failure.Equals(null);

        // Assert
        Assert.False(equal);
    }

    /// <summary>Verifies <see cref="NotAuthorized"/> never equals <see langword="null"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_NotAuthorizedWithNull_ReturnsFalse_Test()
    {
        // Arrange
        var notAuthorized = new NotAuthorized([MissingAdminRole]);

        // Act
        var equal = notAuthorized.Equals(null);

        // Assert
        Assert.False(equal);
    }

    /// <summary>Verifies <see cref="ValidationErrors"/> never equals <see langword="null"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_ValidationErrorsWithNull_ReturnsFalse_Test()
    {
        // Arrange
        var validationErrors = new ValidationErrors([new ValidationError("Name", "required")]);

        // Act
        var equal = validationErrors.Equals(null);

        // Assert
        Assert.False(equal);
    }

    /// <summary>
    /// Verifies two <see cref="NotFound{TId}"/> instances over the same id already compare equal —
    /// unlike the collection-bearing case types above, this needs no override: <c>Id</c> is a
    /// nullable Vogen struct, and Vogen generates real <see cref="IEquatable{T}"/> value equality,
    /// so the record's default generated equality is already correct here.
    /// </summary>
    [Fact]
    public void Equals_NotFoundWithSameId_ReturnsTrueAndEqualHashCodes_Test()
    {
        // Arrange
        var first = new NotFound<ProductId>(SomeProductId);
        var second = new NotFound<ProductId>(SomeProductId);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.Multiple(
            () => Assert.True(equal),
            () => Assert.Equal(first.GetHashCode(), second.GetHashCode())
        );
    }
}

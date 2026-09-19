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
    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    );

    /// <summary>Verifies two <see cref="Failure"/> instances built from equal reasons compare equal.</summary>
    [Fact]
    public void Failure_same_reasons_are_equal()
    {
        // Arrange
        var first = new Failure(["insufficient stock", "product discontinued"]);
        var second = new Failure(["insufficient stock", "product discontinued"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Verifies <see cref="Failure"/> equality doesn't depend on reason order, and that reordered
    /// instances still satisfy the Equals/GetHashCode contract (equal objects must hash equal) —
    /// <see cref="Failure_same_reasons_are_equal"/> only proves that for identically-ordered
    /// reasons, which alone can't catch a hash implementation that is order-sensitive, unlike the
    /// set equality it must agree with.
    /// </summary>
    [Fact]
    public void Failure_reordered_reasons_are_equal_with_equal_hash_codes()
    {
        // Arrange
        var first = new Failure(["a", "b"]);
        var second = new Failure(["b", "a"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>Verifies <see cref="Failure"/> instances with different reasons are not equal.</summary>
    [Fact]
    public void Failure_different_reasons_are_not_equal()
    {
        // Arrange
        var first = new Failure(["insufficient stock"]);
        var second = new Failure(["product discontinued"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies duplicate reasons are collapsed on construction, so a duplicated reason compares equal to a single one.</summary>
    [Fact]
    public void Failure_duplicate_reasons_are_collapsed()
    {
        // Arrange
        var duplicated = new Failure(["a", "a"]);
        var single = new Failure(["a"]);

        // Act
        var equal = duplicated.Equals(single);

        // Assert
        Assert.True(equal);
        Assert.Single(duplicated.Reasons);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a <see cref="Failure"/> built from a <see langword="null"/> reasons sequence has no reasons rather than throwing.</summary>
    [Fact]
    public void Failure_null_reasons_yields_an_empty_collection()
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
    public void NotAuthorized_same_reasons_are_equal()
    {
        // Arrange
        var first = new NotAuthorized(["missing admin role"]);
        var second = new NotAuthorized(["missing admin role"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="NotAuthorized"/> instances with different reasons are not equal.</summary>
    [Fact]
    public void NotAuthorized_different_reasons_are_not_equal()
    {
        // Arrange
        var first = new NotAuthorized(["missing admin role"]);
        var second = new NotAuthorized(["not the owner"]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a <see cref="NotAuthorized"/> built from a <see langword="null"/> reasons sequence has no reasons rather than throwing.</summary>
    [Fact]
    public void NotAuthorized_null_reasons_yields_an_empty_collection()
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
    public void ValidationErrors_same_errors_are_equal()
    {
        // Arrange
        var first = new ValidationErrors([new ValidationError("Name", "required")]);
        var second = new ValidationErrors([new ValidationError("Name", "required")]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="ValidationErrors"/> instances with different field failures are not equal.</summary>
    [Fact]
    public void ValidationErrors_different_errors_are_not_equal()
    {
        // Arrange
        var first = new ValidationErrors([new ValidationError("Name", "required")]);
        var second = new ValidationErrors([new ValidationError("Price", "required")]);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.False(equal);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a <see cref="ValidationErrors"/> built from a <see langword="null"/> sequence has no errors rather than throwing.</summary>
    [Fact]
    public void ValidationErrors_null_errors_yields_an_empty_collection()
    {
        // Arrange
        IEnumerable<ValidationError>? errors = null;

        // Act
        var validationErrors = new ValidationErrors(errors);

        // Assert
        Assert.Empty(validationErrors.Errors);
    }

    /// <summary>
    /// Verifies two <see cref="NotFound{TId}"/> instances over the same id already compare equal —
    /// unlike the collection-bearing case types above, this needs no override: <c>Id</c> is a
    /// nullable Vogen struct, and Vogen generates real <see cref="IEquatable{T}"/> value equality,
    /// so the record's default generated equality is already correct here.
    /// </summary>
    [Fact]
    public void NotFound_same_id_are_equal()
    {
        // Arrange
        var first = new NotFound<ProductId>(SomeProductId);
        var second = new NotFound<ProductId>(SomeProductId);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}

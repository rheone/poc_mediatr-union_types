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
    /// <summary>Verifies two <see cref="Failure"/> instances built from equal reasons compare equal.</summary>
    [Fact]
    public void Failure_instances_with_the_same_reasons_are_equal()
    {
        var first = new Failure(["insufficient stock", "product discontinued"]);
        var second = new Failure(["insufficient stock", "product discontinued"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Verifies <see cref="Failure"/> equality doesn't depend on reason order, and that reordered
    /// instances still satisfy the Equals/GetHashCode contract (equal objects must hash equal) —
    /// <see cref="Failure_instances_with_the_same_reasons_are_equal"/> only proves that for
    /// identically-ordered reasons, which alone can't catch a hash implementation that is
    /// order-sensitive, unlike the set equality it must agree with.
    /// </summary>
    [Fact]
    public void Failure_equality_ignores_reason_order()
    {
        var first = new Failure(["a", "b"]);
        var second = new Failure(["b", "a"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>Verifies <see cref="Failure"/> instances with different reasons are not equal.</summary>
    [Fact]
    public void Failure_instances_with_different_reasons_are_not_equal()
    {
        var first = new Failure(["insufficient stock"]);
        var second = new Failure(["product discontinued"]);

        Assert.NotEqual(first, second);
    }

    /// <summary>Verifies two <see cref="NotAuthorized"/> instances built from equal reasons compare equal.</summary>
    [Fact]
    public void NotAuthorized_instances_with_the_same_reasons_are_equal()
    {
        var first = new NotAuthorized(["missing admin role"]);
        var second = new NotAuthorized(["missing admin role"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>Verifies two <see cref="ValidationErrors"/> instances built from equal field failures compare equal.</summary>
    [Fact]
    public void ValidationErrors_instances_with_the_same_errors_are_equal()
    {
        var first = new ValidationErrors([new ValidationError("Name", "required")]);
        var second = new ValidationErrors([new ValidationError("Name", "required")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Verifies two <see cref="NotFound{TId}"/> instances over the same id already compare equal —
    /// unlike the collection-bearing case types above, this needs no override: <c>Id</c> is a
    /// nullable Vogen struct, and Vogen generates real <see cref="IEquatable{T}"/> value equality,
    /// so the record's default generated equality is already correct here.
    /// </summary>
    [Fact]
    public void NotFound_instances_with_the_same_id_are_equal()
    {
        var id = ProductId.New();

        var first = new NotFound<ProductId>(id);
        var second = new NotFound<ProductId>(id);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}

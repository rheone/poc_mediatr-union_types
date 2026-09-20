using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>
/// C# 14 extension members holding the one definition of what a valid product name and price are,
/// shared by the create, update and patch validators so the three cannot drift apart.
/// </summary>
internal static class ProductRuleExtensions
{
    /// <summary>The longest display name a product may have.</summary>
    public const int MaxNameLength = 200;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        /// <summary>Requires a non-blank name of at most <see cref="MaxNameLength"/> characters.</summary>
        /// <returns>The rule builder, for chaining.</returns>
        public IRuleBuilderOptions<T, string> MustBeValidProductName() =>
            rule.NotEmpty().MaximumLength(MaxNameLength);
    }

    extension<T>(IRuleBuilder<T, decimal> rule)
    {
        /// <summary>Requires a price of zero or more.</summary>
        /// <returns>The rule builder, for chaining.</returns>
        public IRuleBuilderOptions<T, decimal> MustBeValidProductPrice() =>
            rule.GreaterThanOrEqualTo(0);
    }
}

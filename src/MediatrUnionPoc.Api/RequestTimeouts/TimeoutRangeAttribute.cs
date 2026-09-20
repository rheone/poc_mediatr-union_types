using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>
/// Validates a <see cref="TimeSpan"/> option lies within an inclusive range given in the invariant
/// <c>[-][d.]hh:mm:ss[.fffffff]</c> form. A dedicated attribute (rather than <see cref="RangeAttribute"/>
/// with a type) keeps the limits culture-independent, so a host running under any culture validates the
/// same way.
/// </summary>
/// <param name="minimum">The smallest valid value, inclusive.</param>
/// <param name="maximum">The largest valid value, inclusive.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TimeoutRangeAttribute(string minimum, string maximum) : ValidationAttribute
{
    private readonly TimeSpan _minimum = TimeSpan.Parse(minimum, CultureInfo.InvariantCulture);

    private readonly TimeSpan _maximum = TimeSpan.Parse(maximum, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is TimeSpan span && span >= _minimum && span <= _maximum)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            $"{validationContext.DisplayName} must be a time span from {_minimum.ToString("c", CultureInfo.InvariantCulture)} to {_maximum.ToString("c", CultureInfo.InvariantCulture)}.",
            [validationContext.MemberName ?? validationContext.DisplayName]
        );
    }
}

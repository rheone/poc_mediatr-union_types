using FluentValidation;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Features.Products.Patch;

/// <summary>Validates <see cref="PatchProductCommand"/> before <see cref="PatchProductHandler"/> runs.</summary>
public sealed class PatchProductValidator : AbstractValidator<PatchProductCommand>
{
    /// <summary>
    /// Requires a real id, at least one supplied field, and every supplied field to obey the same
    /// rules create and update apply (<see cref="ProductRuleExtensions"/>). A supplied-but-<see langword="null"/>
    /// field fails like an empty one: a required field cannot be cleared. Absent fields are not
    /// validated at all. Errors are keyed by the command's property name (<c>Name</c>,
    /// <c>Price</c>) exactly as for update.
    /// </summary>
    public PatchProductValidator()
    {
        const string nameProperty = nameof(PatchProductCommand.Name);
        const string priceProperty = nameof(PatchProductCommand.Price);

        RuleFor(x => x.Id).NotEqual(Guid.Empty);

        RuleFor(x => x)
            .Must(x => x.Name.IsPresent || x.Price.IsPresent)
            .WithMessage("The patch must supply at least one of name or price.")
            .OverridePropertyName(string.Empty);

        RuleFor(x => x.Name.Value!)
            .MustBeValidProductName()
            .WithName(nameProperty)
            .OverridePropertyName(nameProperty)
            .When(x => x.Name.IsPresent);

        RuleFor(x => x.Price.Value)
            .NotNull()
            .WithName(priceProperty)
            .OverridePropertyName(priceProperty)
            .When(x => x.Price.IsPresent);

        RuleFor(x => x.Price.Value.GetValueOrDefault())
            .MustBeValidProductPrice()
            .WithName(priceProperty)
            .OverridePropertyName(priceProperty)
            .When(x => x.Price.IsPresent && x.Price.Value is not null);
    }
}

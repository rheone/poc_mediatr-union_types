using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.GetById;

/// <summary>Validates <see cref="GetProductByIdQuery"/> before <see cref="GetProductByIdHandler"/> runs.</summary>
public sealed class GetProductByIdValidator : AbstractValidator<GetProductByIdQuery>
{
    /// <summary>
    /// Rejects <see cref="Guid.Empty"/> as an id — the only input shape this query has to validate
    /// before <see cref="GetProductByIdHandler"/> attempts the lookup.
    /// </summary>
    public GetProductByIdValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
    }
}

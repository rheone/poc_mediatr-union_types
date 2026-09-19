using MediatR;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>
/// Creates a product unconditionally — <see cref="CreateProductCommand"/> has already passed
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.ValidationBehavior{TRequest,TResponse}"/>
/// by the time this runs, so there is no failure case to check for here beyond the ones the
/// union simply doesn't declare (see <see cref="CreateProductResult"/>).
/// </summary>
public sealed class CreateProductHandler(IProductRepository repository)
    : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    /// <inheritdoc/>
    public async Task<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var product = Product.Create(request.Name, Money.From(request.Price));
        await repository.AddAsync(product, cancellationToken);
        return ProductDto.FromDomain(product);
    }
}

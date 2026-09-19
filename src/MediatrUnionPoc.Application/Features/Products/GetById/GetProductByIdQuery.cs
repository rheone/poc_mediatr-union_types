using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.GetById;

/// <summary>Looks up a single product by id.</summary>
/// <param name="Id">The product's identity, as a raw <see cref="Guid"/> — converted to <see cref="MediatrUnionPoc.Domain.ProductId"/> inside <see cref="GetProductByIdHandler"/>, after <see cref="GetProductByIdValidator"/> has confirmed it isn't <see cref="Guid.Empty"/>.</param>
public sealed record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdResult>;

namespace MediatrUnionPoc.Api.Contracts;

/// <summary>Request body for <see cref="Controllers.ProductsController.CreateAsync"/>.</summary>
public sealed record CreateProductRequest(string Name, decimal Price);

/// <summary>Request body for <see cref="Controllers.ProductsController.UpdateAsync"/>.</summary>
public sealed record UpdateProductRequest(string Name, decimal Price);

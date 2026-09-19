namespace MediatrUnionPoc.Api.Contracts;

/// <summary>Request body for <see cref="Controllers.ProductsController.CreateAsync"/>.</summary>
public sealed record CreateProductRequest(string Name, decimal Price);

/// <summary>Request body for <see cref="Controllers.ProductsController.UpdateAsync"/>.</summary>
public sealed record UpdateProductRequest(string Name, decimal Price);

/// <summary>A minimal, non-RFC-7807 error body used for the <c>NotFound</c> case — see <see cref="Controllers.ProductsController"/>'s <c>ValidationProblemFrom</c> for the RFC 7807 path validation errors take instead.</summary>
public sealed record ProblemPayload(string Message, string Code);

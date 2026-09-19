using System.Security.Claims;
using MediatR;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.GetById;
using MediatrUnionPoc.Application.Features.Products.GetPaged;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MediatrUnionPoc.Api.Controllers;

/// <summary>
/// Basic product CRUD. Every action's only job is to <c>switch</c> on the union MediatR returns
/// and translate each case into a concrete HTTP response — see the README's "switch-and-unwrap"
/// section for why that translation never skips this step.
/// </summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// The request header this POC accepts as proof of administrator identity, in place of real
    /// authentication. A value of <c>"true"</c> (case-insensitive) grants the caller the
    /// <c>Administrator</c> role for the duration of the request.
    /// </summary>
    public const string AdminHeaderName = "X-Admin";

    /// <summary>Creates a product.</summary>
    /// <param name="request">The product to create.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 201 with the created <see cref="ProductDto"/>; 400 with per-field errors if <paramref name="request"/>
    /// fails validation; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new CreateProductCommand(request.Name, request.Price),
            cancellationToken
        );

        return result switch
        {
            ProductDto dto => CreatedAtAction(nameof(GetByIdAsync), new { id = dto.Id.Value }, dto),
            ValidationErrors errors => ValidationProblemFrom(errors),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>Looks up a single product by id.</summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>200 with the <see cref="ProductDto"/>; 404 if it doesn't exist; 500 for any other <see cref="Error"/> case.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemPayload), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(new GetProductByIdQuery(id), cancellationToken);

        return result switch
        {
            ProductDto dto => Ok(dto),
            NotFoundCase notFound => NotFound(
                new ProblemPayload($"Product '{notFound.Id}' was not found.", "NOT_FOUND")
            ),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>Lists products a page at a time, ordered by name.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>200 with a <see cref="PagedResult{T}"/> of <see cref="ProductDto"/>; 400 if paging parameters are out of range.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new GetPagedProductsQuery(pageNumber, pageSize),
            cancellationToken
        );

        return result switch
        {
            PagedResult<ProductDto> page => Ok(page),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>Replaces a product's name and price.</summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="request">The new name and price.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>204 on success; 404 if the product doesn't exist; 400 on validation failure; 500 for any other <see cref="Error"/> case.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemPayload), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new UpdateProductCommand(id, request.Name, request.Price),
            cancellationToken
        );

        return result switch
        {
            Success => NoContent(),
            NotFoundCase notFound => NotFound(
                new ProblemPayload($"Product '{notFound.Id}' was not found.", "NOT_FOUND")
            ),
            ValidationErrors errors => ValidationProblemFrom(errors),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>
    /// Deletes a product. Only an administrator may delete — this POC has no real
    /// authentication, so the caller proves administrator identity by sending an
    /// <c>X-Admin: true</c> request header; see <see cref="AdminHeaderName"/>.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="adminHeader">The <see cref="AdminHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 204 on success; 403 if the caller isn't an administrator; 404 if the product doesn't
    /// exist; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemPayload), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        [FromHeader(Name = AdminHeaderName)] string? adminHeader,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new DeleteProductCommand(id, CallerPrincipal(adminHeader)),
            cancellationToken
        );

        return result switch
        {
            Success => NoContent(),
            NotFoundCase notFound => NotFound(
                new ProblemPayload($"Product '{notFound.Id}' was not found.", "NOT_FOUND")
            ),
            NotAuthorized notAuthorized => Problem(
                detail: string.Join("; ", notAuthorized.Reasons),
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden"
            ),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>Projects a <see cref="ValidationErrors"/> case into an RFC 7807 validation problem response.</summary>
    private ActionResult ValidationProblemFrom(ValidationErrors errors)
    {
        foreach (var error in errors.Errors)
        {
            ModelState.AddModelError(error.PropertyName ?? string.Empty, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }

    /// <summary>
    /// Builds the caller's <see cref="ClaimsPrincipal"/> from the <see cref="AdminHeaderName"/>
    /// header's bound value — the only identity source this POC has, in place of real
    /// authentication.
    /// </summary>
    /// <param name="adminHeader">The <see cref="AdminHeaderName"/> header's value, or <see langword="null"/> if absent.</param>
    /// <returns>A principal with an <c>Administrator</c> role claim when <paramref name="adminHeader"/> is <c>"true"</c>; otherwise an anonymous principal.</returns>
    private static ClaimsPrincipal CallerPrincipal(string? adminHeader)
    {
        var identity = new ClaimsIdentity(authenticationType: "Header");

        if (string.Equals(adminHeader, "true", StringComparison.OrdinalIgnoreCase))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
        }

        return new ClaimsPrincipal(identity);
    }
}

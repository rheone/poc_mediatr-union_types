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
/// Product CRUD. Each action sends one request and <c>switch</c>es exhaustively over the union it
/// returns; no expected outcome is signalled by an exception. Case-to-status mapping (also declared
/// via <c>ProducesResponseType</c> on each action):
/// <list type="table">
/// <listheader><term>Action</term><description>Cases</description></listheader>
/// <item><term>CreateAsync</term><description>ProductDto 201; ValidationErrors 400; Error 500.</description></item>
/// <item><term>GetByIdAsync</term><description>ProductDto 200; NotFound 404; Error 500.</description></item>
/// <item><term>GetPagedAsync</term><description>PagedResult 200; Error with <see cref="Error.ValidationFailureCode"/> 400; other Error 500.</description></item>
/// <item><term>UpdateAsync</term><description>Success 204; NotFound 404; ValidationErrors 400; NotAuthorized 403; Error 500.</description></item>
/// <item><term>DeleteAsync</term><description>Success 204; NotFound 404; NotAuthorized 403; Error 500.</description></item>
/// </list>
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

    /// <summary>
    /// The request header this POC accepts as proof of caller identity, in place of real
    /// authentication. Its value becomes the caller's <see cref="ClaimTypes.NameIdentifier"/>
    /// claim — the identity <see cref="Application.Common.Authorization.OwnerAuthorizationHandler{TResource}"/>
    /// compares against a resource's owner, e.g. <see cref="Domain.Product.OwnerId"/>. A caller
    /// who created a product with a given <see cref="CallerIdHeaderName"/> value must present the
    /// same value to update or delete it.
    /// </summary>
    public const string CallerIdHeaderName = "X-Caller-Id";

    /// <summary>
    /// Creates a product. The caller becomes the product's owner by presenting the
    /// <see cref="CallerIdHeaderName"/> header — see that constant, and
    /// <see cref="Domain.Product.OwnerId"/> for what ownership then gates.
    /// </summary>
    /// <param name="request">The product to create.</param>
    /// <param name="callerIdHeader">The <see cref="CallerIdHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
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
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new CreateProductCommand(
                request.Name,
                request.Price,
                CallerPrincipal(adminHeader: null, callerIdHeader)
            ),
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
    /// <returns>
    /// 200 with a <see cref="PagedResult{T}"/> of <see cref="ProductDto"/>; 400 if paging parameters
    /// are out of range (the union reports that as an <see cref="Error"/> whose code is
    /// <see cref="Error.ValidationFailureCode"/>); 500 for any other <see cref="Error"/>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            Error { Code: Error.ValidationFailureCode } error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: error.Code
            ),
            Error error => Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code
            ),
        };
    }

    /// <summary>
    /// Replaces a product's name and price. Only the product's owner may update it — this POC has
    /// no real authentication, so the caller proves identity by sending an
    /// <see cref="CallerIdHeaderName"/> header whose value must match the value presented when the
    /// product was created; see <see cref="CallerIdHeaderName"/>.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="request">The new name and price.</param>
    /// <param name="callerIdHeader">The <see cref="CallerIdHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 204 on success; 404 if the product doesn't exist; 400 on validation failure; 403 if the
    /// caller doesn't own the product; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemPayload), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new UpdateProductCommand(
                id,
                request.Name,
                request.Price,
                CallerPrincipal(adminHeader: null, callerIdHeader)
            ),
            cancellationToken
        );

        return result switch
        {
            Success => NoContent(),
            NotFoundCase notFound => NotFound(
                new ProblemPayload($"Product '{notFound.Id}' was not found.", "NOT_FOUND")
            ),
            ValidationErrors errors => ValidationProblemFrom(errors),
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

    /// <summary>
    /// Deletes a product. Either the product's owner or an administrator may delete it — this POC
    /// has no real authentication, so the caller proves owner identity via the
    /// <see cref="CallerIdHeaderName"/> header (the same one <see cref="UpdateAsync"/> uses) and/or
    /// administrator identity via an <c>X-Admin: true</c> header; see
    /// <see cref="AdminHeaderName"/>.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="adminHeader">The <see cref="AdminHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="callerIdHeader">The <see cref="CallerIdHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 204 on success; 403 if the caller neither owns the product nor is an administrator; 404 if
    /// the product doesn't exist; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemPayload), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        [FromHeader(Name = AdminHeaderName)] string? adminHeader,
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        CancellationToken cancellationToken = default
    )
    {
        var result = await sender.Send(
            new DeleteProductCommand(id, CallerPrincipal(adminHeader, callerIdHeader)),
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
    /// and <see cref="CallerIdHeaderName"/> headers' bound values — the only identity source this
    /// POC has, in place of real authentication.
    /// </summary>
    /// <param name="adminHeader">The <see cref="AdminHeaderName"/> header's value, or <see langword="null"/> if absent.</param>
    /// <param name="callerIdHeader">The <see cref="CallerIdHeaderName"/> header's value, or <see langword="null"/> if absent.</param>
    /// <returns>
    /// A principal with an <c>Administrator</c> role claim when <paramref name="adminHeader"/> is
    /// <c>"true"</c>, and/or a <see cref="ClaimTypes.NameIdentifier"/> claim when
    /// <paramref name="callerIdHeader"/> is non-empty; an anonymous principal if neither is present.
    /// </returns>
    private static ClaimsPrincipal CallerPrincipal(string? adminHeader, string? callerIdHeader)
    {
        var identity = new ClaimsIdentity(authenticationType: "Header");

        if (string.Equals(adminHeader, "true", StringComparison.OrdinalIgnoreCase))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
        }

        if (!string.IsNullOrEmpty(callerIdHeader))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, callerIdHeader));
        }

        return new ClaimsPrincipal(identity);
    }
}

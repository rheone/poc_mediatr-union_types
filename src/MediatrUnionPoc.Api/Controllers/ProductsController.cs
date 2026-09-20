using System.Security.Claims;
using MediatR;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.OpenApi;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.GetById;
using MediatrUnionPoc.Application.Features.Products.GetPaged;
using MediatrUnionPoc.Application.Features.Products.Patch;
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
/// <item><term>CreateAsync</term><description>ProductDto 201 (with <c>ETag</c>); ValidationErrors 400; Conflict 409 (duplicate product name); Error 500.</description></item>
/// <item><term>GetByIdAsync</term><description>ProductDto 200 (with <c>ETag</c>); NotFound 404; Error 500.</description></item>
/// <item><term>GetPagedAsync</term><description>PagedResult 200 (with <c>X-Total-Count</c> and <c>Link</c>); ValidationErrors 400 (per-field); Error 500.</description></item>
/// <item><term>UpdateAsync</term><description>ProductDto 204 (with the new <c>ETag</c>); NotFound 404; ValidationErrors 400 (also a malformed <c>If-Match</c>); NotAuthorized 403; Conflict 409 (duplicate product name); PreconditionFailed 412; missing <c>If-Match</c> 428; Error 500.</description></item>
/// <item><term>PatchAsync</term><description>ProductDto 200 (with the new <c>ETag</c>); NotFound 404; ValidationErrors 400 (also a malformed <c>If-Match</c>); NotAuthorized 403; Conflict 409 (duplicate product name); PreconditionFailed 412; a body that is not <c>application/merge-patch+json</c> 415; missing <c>If-Match</c> 428; Error 500.</description></item>
/// <item><term>DeleteAsync</term><description>Success 204; NotFound 404; NotAuthorized 403; PreconditionFailed 412; malformed <c>If-Match</c> 400; Error 500.</description></item>
/// </list>
/// </summary>
/// <param name="sender">The MediatR sender every action dispatches its request through.</param>
/// <exception cref="ArgumentNullException"><paramref name="sender"/> is <see langword="null"/>.</exception>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender ?? throw new ArgumentNullException(nameof(sender));

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
    /// same value to update it.
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
    /// 201 with the created <see cref="ProductDto"/> and its <c>ETag</c>; 400 with per-field errors if <paramref name="request"/>
    /// fails validation; 409 if another product already has an equivalent name (ignoring case and surrounding whitespace); 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPost]
    [ReturnsETag]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAsync(
        CreateProductRequest request,
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _sender.Send(
            new CreateProductCommand(
                request.Name,
                request.Price,
                ClaimsPrincipal.FromCallerHeaders(adminHeader: null, callerIdHeader)
            ),
            cancellationToken
        );

        return result switch
        {
            ProductDto dto => WithETag(
                dto,
                CreatedAtAction(nameof(GetByIdAsync), new { id = dto.Id.Value }, dto)
            ),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
            Conflict conflict => conflict.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
        };
    }

    /// <summary>Looks up a single product by id.</summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>200 with the <see cref="ProductDto"/> and its <c>ETag</c>; 400 if the id fails validation (an <see cref="Error"/> coded <see cref="Error.ValidationFailureCode"/>); 404 if it doesn't exist; 500 for any other <see cref="Error"/> case.</returns>
    [HttpGet("{id:guid}")]
    [ReturnsETag]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _sender.Send(new GetProductByIdQuery(id), cancellationToken);

        return result switch
        {
            ProductDto dto => WithETag(dto, Ok(dto)),
            NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product"),
            Error error => error.ToProblemResult(HttpContext),
        };
    }

    /// <summary>
    /// Lists products: filtered, sorted and a page at a time. See <see cref="ListProductsRequest"/>
    /// for the query-string contract.
    /// </summary>
    /// <param name="request">The filters, sort and paging bound from the query string.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 200 with a <see cref="PagedResult{T}"/> of <see cref="ProductDto"/> (an empty page, with correct
    /// metadata, when the page is past the end), plus <c>X-Total-Count</c> and RFC 8288 <c>Link</c>
    /// headers; 400 with per-field errors if a query parameter is malformed or out of range; 500 for
    /// any <see cref="Error"/>.
    /// </returns>
    [HttpGet]
    [ReturnsPagingHeaders]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] ListProductsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _sender.Send(
            new GetPagedProductsQuery(
                request.PageNumber,
                request.PageSize,
                request.NameContains,
                request.MinPrice,
                request.MaxPrice,
                request.OwnerId,
                request.Sort
            ),
            cancellationToken
        );

        return result switch
        {
            PagedResult<ProductDto> page => WithPagingHeaders(page, Ok(page)),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
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
    /// <param name="ifMatchHeader">The <c>If-Match</c> request header: the ETag of the version being replaced. Required.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 204 on success, with the product's new <c>ETag</c>; 404 if the product doesn't exist; 400 on
    /// validation failure or a malformed <c>If-Match</c>; 403 if the caller doesn't own the product;
    /// 409 if the new name duplicates another product's; 412 if <c>If-Match</c> no longer names the product's version; 428 if <c>If-Match</c> is
    /// absent; 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPut("{id:guid}")]
    [ReturnsETag]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatchHeader,
        CancellationToken cancellationToken = default
    )
    {
        return WithRequiredVersionAsync(
            ifMatchHeader,
            expectedVersion =>
                SendUpdateAsync(id, request, callerIdHeader, expectedVersion, cancellationToken)
        );
    }

    /// <summary>
    /// Changes only the fields of a product the caller supplies (JSON Merge Patch, RFC 7396). Same
    /// ownership rule, <c>If-Match</c> requirement and duplicate-name rule as
    /// <see cref="UpdateAsync"/>; the body must be sent as <c>application/merge-patch+json</c>.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="request">The members to change; absent members are left alone, a <c>null</c> member or an empty patch is a 400.</param>
    /// <param name="callerIdHeader">The <see cref="CallerIdHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="ifMatchHeader">The <c>If-Match</c> request header: the ETag of the version being changed. Required.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 200 with the patched <see cref="ProductDto"/> and its new <c>ETag</c>; 400 on validation failure
    /// or a malformed <c>If-Match</c>; 403 if the caller does not own the product; 404 if it does not
    /// exist; 409 if the new name duplicates another product's; 412 if <c>If-Match</c> no longer
    /// names the product's version; 415 for any other media type; 428 if <c>If-Match</c> is absent;
    /// 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpPatch("{id:guid}")]
    [Consumes(MediaTypes.MergePatchJson)]
    [ReturnsETag]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> PatchAsync(
        Guid id,
        PatchProductRequest request,
        [FromHeader(Name = CallerIdHeaderName)] string? callerIdHeader,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatchHeader,
        CancellationToken cancellationToken = default
    )
    {
        return WithRequiredVersionAsync(
            ifMatchHeader,
            expectedVersion =>
                SendPatchAsync(id, request, callerIdHeader, expectedVersion, cancellationToken)
        );
    }

    /// <summary>Runs <paramref name="send"/> with the version a <em>required</em> <c>If-Match</c> header names, or answers 428 (absent) or 400 (malformed) without sending anything.</summary>
    private async Task<IActionResult> WithRequiredVersionAsync(
        string? ifMatchHeader,
        Func<ProductVersion, Task<IActionResult>> send
    )
    {
        return IfMatchHeader.Parse(ifMatchHeader) switch
        {
            ProductVersion expectedVersion => await send(expectedVersion),
            MissingIfMatch missing => missing.ToProblemResult(HttpContext),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
        };
    }

    private async Task<IActionResult> SendPatchAsync(
        Guid id,
        PatchProductRequest request,
        string? callerIdHeader,
        ProductVersion expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var result = await _sender.Send(
            new PatchProductCommand(
                id,
                request.Name,
                request.Price,
                ClaimsPrincipal.FromCallerHeaders(adminHeader: null, callerIdHeader),
                expectedVersion
            ),
            cancellationToken
        );

        return result switch
        {
            ProductDto patched => WithETag(patched, Ok(patched)),
            NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product"),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
            NotAuthorized notAuthorized => notAuthorized.ToProblemResult(HttpContext),
            PreconditionFailed stale => stale.ToProblemResult(HttpContext),
            Conflict conflict => conflict.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
        };
    }

    private IActionResult WithPagingHeaders(PagedResult<ProductDto> page, IActionResult result)
    {
        Response.SetPagingHeaders(page);
        return result;
    }

    private IActionResult WithETag(ProductDto dto, IActionResult result)
    {
        Response.SetETag(dto.Version);
        return result;
    }

    private async Task<IActionResult> SendUpdateAsync(
        Guid id,
        UpdateProductRequest request,
        string? callerIdHeader,
        ProductVersion expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var result = await _sender.Send(
            new UpdateProductCommand(
                id,
                request.Name,
                request.Price,
                ClaimsPrincipal.FromCallerHeaders(adminHeader: null, callerIdHeader),
                expectedVersion
            ),
            cancellationToken
        );

        return result switch
        {
            ProductDto updated => WithETag(updated, NoContent()),
            NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product"),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
            NotAuthorized notAuthorized => notAuthorized.ToProblemResult(HttpContext),
            PreconditionFailed stale => stale.ToProblemResult(HttpContext),
            Conflict conflict => conflict.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
        };
    }

    /// <summary>
    /// Deletes a product. Only an administrator may delete — this POC has no real
    /// authentication, so the caller proves administrator identity by sending an
    /// <c>X-Admin: true</c> request header; see <see cref="AdminHeaderName"/>.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="adminHeader">The <see cref="AdminHeaderName"/> request header, bound directly rather than read off <c>Request.Headers</c>.</param>
    /// <param name="ifMatchHeader">The optional <c>If-Match</c> request header; when present the delete only proceeds if it names the product's current version.</param>
    /// <param name="cancellationToken">Bound automatically from the incoming request; defaults to <see cref="CancellationToken.None"/> for direct calls.</param>
    /// <returns>
    /// 204 on success; 400 if the id fails validation (an <see cref="Error"/> coded
    /// <see cref="Error.ValidationFailureCode"/>) or <c>If-Match</c> is malformed; 403 if the caller
    /// isn't an administrator; 404 if the product doesn't exist; 412 if <c>If-Match</c> is stale;
    /// 500 for any other <see cref="Error"/> case.
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        [FromHeader(Name = AdminHeaderName)] string? adminHeader,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatchHeader,
        CancellationToken cancellationToken = default
    )
    {
        return IfMatchHeader.Parse(ifMatchHeader) switch
        {
            ProductVersion expectedVersion => await SendDeleteAsync(
                id,
                adminHeader,
                expectedVersion,
                cancellationToken
            ),

            // Optional on delete: no header means "delete whatever version is stored".
            MissingIfMatch => await SendDeleteAsync(id, adminHeader, null, cancellationToken),
            ValidationErrors errors => errors.ToProblemResult(HttpContext),
        };
    }

    private async Task<IActionResult> SendDeleteAsync(
        Guid id,
        string? adminHeader,
        ProductVersion? expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var result = await _sender.Send(
            new DeleteProductCommand(
                id,
                ClaimsPrincipal.FromCallerHeaders(adminHeader, callerIdHeader: null),
                expectedVersion
            ),
            cancellationToken
        );

        return result switch
        {
            Success => NoContent(),
            NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product"),
            NotAuthorized notAuthorized => notAuthorized.ToProblemResult(HttpContext),
            PreconditionFailed stale => stale.ToProblemResult(HttpContext),
            Error error => error.ToProblemResult(HttpContext),
        };
    }
}

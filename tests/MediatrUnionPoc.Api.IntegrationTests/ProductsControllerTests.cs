using System.Net;
using System.Net.Http.Json;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the one seam nothing else in the suite crosses: the real ASP.NET Core pipeline,
/// through real routing and the real MediatR pipeline behaviors, to the union-to-HTTP-status
/// mapping each controller action's <c>switch</c> performs. A fresh <see cref="ProductsApiFactory"/>
/// per test gives each test its own InMemory database, so tests never see each other's data.
/// </summary>
public sealed class ProductsControllerTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ProductsControllerTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public ProductsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    /// <summary>Disposes the test's <see cref="HttpClient"/> and its backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies a create returns 201 with the created product and a Location header that resolves to it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Create_with_valid_body_returns_201_and_resolvable_location_header()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(dto);
        Assert.Equal("Widget", dto.Name);

        // CreatedAtAction resolves its target by action name; the Location header only works if
        // nameof(GetByIdAsync) matches the name MVC registered (SuppressAsyncSuffixInActionNames = false).
        using var located = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, located.StatusCode);
    }

    /// <summary>Verifies an invalid create request returns 400 with per-field validation errors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Create_with_invalid_body_returns_400_with_per_field_errors()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(string.Empty, -5m)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name", body);
        Assert.Contains("Price", body);
    }

    /// <summary>Verifies a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetById_for_missing_product_returns_404()
    {
        using var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies paging only returns products from this test's own isolated database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPaged_after_one_create_returns_only_that_product()
    {
        using var createResponse = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );

        using var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("Widget", page.Items[0].Name);
    }

    /// <summary>Verifies out-of-range paging parameters return 400 rather than 500.</summary>
    /// <param name="query">The query string carrying invalid paging parameters.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("pageNumber=0&pageSize=10")]
    [InlineData("pageNumber=1&pageSize=0")]
    [InlineData("pageNumber=1&pageSize=101")]
    public async Task GetPaged_with_out_of_range_paging_returns_400(string query)
    {
        using var response = await _client.GetAsync($"/api/products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies updating a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_for_missing_product_returns_404()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}",
            new UpdateProductRequest("Widget Pro", 19.99m)
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an owner's update with an invalid body returns 400 with per-field validation errors (authorization runs before validation, so the caller must own the product).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_by_owner_with_invalid_body_returns_400_with_per_field_errors()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{dto!.Id.Value}")
        {
            Content = JsonContent.Create(new UpdateProductRequest(string.Empty, -5m)),
        };
        request.Headers.Add("X-Caller-Id", "owner-1");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name", body);
        Assert.Contains("Price", body);
    }

    /// <summary>Verifies an owner's update returns 204 and its changes are visible to a subsequent GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_by_owner_returns_204_and_persists_changes()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var updateResponse = await PutAsCallerAsync(
            $"/api/products/{dto!.Id.Value}",
            "owner-1"
        );
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var fetched = await _client.GetFromJsonAsync<ProductDto>($"/api/products/{dto.Id.Value}");
        Assert.Equal("Widget Pro", fetched!.Name);
        Assert.Equal(19.99m, fetched.Price);
    }

    /// <summary>Verifies a non-owner's update returns 403 and leaves the product untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_by_non_owner_returns_403_and_leaves_product_untouched()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var updateResponse = await PutAsCallerAsync(
            $"/api/products/{dto!.Id.Value}",
            "owner-2"
        );
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        var fetched = await _client.GetFromJsonAsync<ProductDto>($"/api/products/{dto.Id.Value}");
        Assert.Equal("Widget", fetched!.Name);
        Assert.Equal(9.99m, fetched.Price);
    }

    /// <summary>Verifies an update with no caller identity at all returns 403, since an anonymous caller never matches any product's owner.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_without_caller_id_header_returns_403()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var updateResponse = await _client.PutAsJsonAsync(
            $"/api/products/{dto!.Id.Value}",
            new UpdateProductRequest("Widget Pro", 19.99m)
        );

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    /// <summary>
    /// Verifies an anonymous caller updating a product that was itself created without an owner
    /// (an empty <see cref="Domain.Product.OwnerId"/>) still returns 403 — guards against a
    /// regression where an absent caller claim and an unowned product's empty owner id could
    /// compare equal and wrongly authorize.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_of_unowned_product_by_anonymous_caller_returns_403()
    {
        using var created = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var updateResponse = await _client.PutAsJsonAsync(
            $"/api/products/{dto!.Id.Value}",
            new UpdateProductRequest("Widget Pro", 19.99m)
        );

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    /// <summary>Verifies an administrator's delete returns 204 and the product subsequently returns 404 from GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_by_administrator_returns_204_and_product_is_then_gone()
    {
        using var created = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var deleteResponse = await DeleteAsAdminAsync($"/api/products/{dto!.Id.Value}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var afterDelete = await _client.GetAsync($"/api/products/{dto.Id.Value}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    /// <summary>Verifies an administrator deleting a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_for_missing_product_returns_404()
    {
        using var response = await DeleteAsAdminAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies a delete without the X-Admin header or a matching X-Caller-Id returns 403, and the product is left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_without_admin_or_owner_headers_returns_403_and_leaves_product_untouched()
    {
        using var created = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var deleteResponse = await _client.DeleteAsync($"/api/products/{dto!.Id.Value}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        using var afterDelete = await _client.GetAsync($"/api/products/{dto.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode);
    }

    /// <summary>Verifies the product's owner can delete it without the X-Admin header, via the X-Caller-Id header alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_by_owner_without_admin_header_returns_204()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var deleteResponse = await DeleteAsCallerAsync(
            $"/api/products/{dto!.Id.Value}",
            "owner-1"
        );
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var afterDelete = await _client.GetAsync($"/api/products/{dto.Id.Value}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    /// <summary>Verifies a caller who neither owns the product nor presents the X-Admin header gets 403, and the product is left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_by_non_owner_non_administrator_returns_403_and_leaves_product_untouched()
    {
        using var created = await PostAsCallerAsync("owner-1");
        var dto = await created.Content.ReadFromJsonAsync<ProductDto>();

        using var deleteResponse = await DeleteAsCallerAsync(
            $"/api/products/{dto!.Id.Value}",
            "owner-2"
        );
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        using var afterDelete = await _client.GetAsync($"/api/products/{dto.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode);
    }

    /// <summary>Sends a DELETE request carrying the <c>X-Admin: true</c> header the API treats as proof of administrator identity.</summary>
    /// <param name="requestUri">The request URI.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> DeleteAsAdminAsync(string requestUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Add("X-Admin", "true");
        return await _client.SendAsync(request);
    }

    /// <summary>Sends a DELETE request carrying the <c>X-Caller-Id</c> header the API treats as proof of caller identity.</summary>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="callerId">The <c>X-Caller-Id</c> header value to present.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> DeleteAsCallerAsync(string requestUri, string callerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Add("X-Caller-Id", callerId);
        return await _client.SendAsync(request);
    }

    /// <summary>
    /// Creates a "Widget" product carrying the <c>X-Caller-Id</c> header the API treats as proof
    /// of caller identity, so the created product's owner is <paramref name="callerId"/>.
    /// </summary>
    /// <param name="callerId">The <c>X-Caller-Id</c> header value to present.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> PostAsCallerAsync(string callerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new CreateProductRequest("Widget", 9.99m)),
        };
        request.Headers.Add("X-Caller-Id", callerId);
        return await _client.SendAsync(request);
    }

    /// <summary>Sends a PUT request carrying the <c>X-Caller-Id</c> header the API treats as proof of caller identity.</summary>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="callerId">The <c>X-Caller-Id</c> header value to present.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> PutAsCallerAsync(string requestUri, string callerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(new UpdateProductRequest("Widget Pro", 19.99m)),
        };
        request.Headers.Add("X-Caller-Id", callerId);
        return await _client.SendAsync(request);
    }
}

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
    public async Task Create_returns_201_with_the_created_product_and_a_working_location_header()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Widget", 9.99m)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(dto);
        Assert.Equal("Widget", dto.Name);

        // CreatedAtAction's Location header must resolve back to the same resource — proves
        // nameof(GetByIdAsync) still lines up with the actual route after the Async-suffix rename.
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
    public async Task GetById_returns_404_for_a_product_that_does_not_exist()
    {
        using var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies paging only returns products from this test's own isolated database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPaged_returns_only_products_created_in_this_tests_own_database()
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

    /// <summary>Verifies updating a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_returns_404_for_a_product_that_does_not_exist()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}",
            new UpdateProductRequest("Widget Pro", 19.99m)
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an update returns 204 and its changes are visible to a subsequent GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Update_persists_changes_that_a_subsequent_GetById_can_see()
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
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var fetched = await _client.GetFromJsonAsync<ProductDto>($"/api/products/{dto.Id.Value}");
        Assert.Equal("Widget Pro", fetched!.Name);
        Assert.Equal(19.99m, fetched.Price);
    }

    /// <summary>Verifies an administrator's delete returns 204 and the product subsequently returns 404 from GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_removes_the_product_so_a_subsequent_GetById_returns_404()
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
    public async Task Delete_returns_404_for_a_product_that_does_not_exist()
    {
        using var response = await DeleteAsAdminAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies a delete without the X-Admin header returns 403, and the product is left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_without_the_admin_header_returns_403_and_leaves_the_product_untouched()
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

    /// <summary>Sends a DELETE request carrying the <c>X-Admin: true</c> header the API treats as proof of administrator identity.</summary>
    /// <param name="requestUri">The request URI.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> DeleteAsAdminAsync(string requestUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Add("X-Admin", "true");
        return await _client.SendAsync(request);
    }
}

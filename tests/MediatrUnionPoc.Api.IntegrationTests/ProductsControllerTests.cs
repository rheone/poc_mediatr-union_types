using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Controllers;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the one seam nothing else in the suite crosses: the real ASP.NET Core pipeline,
/// through real routing and the real MediatR pipeline behaviors, to the union-to-HTTP-status
/// mapping each controller action's <c>switch</c> performs. A fresh <see cref="ProductsApiFactory"/>
/// per test gives each test its own SQLite in-memory database, so tests never see each other's data.
/// </summary>
/// <remarks>
/// <see cref="ProductsController"/> is exercised over HTTP, so a null request body is answered by
/// the framework's model binding with 400 rather than an <see cref="ArgumentNullException"/>; the
/// action's model-bound <c>request</c> parameters are deliberately left unguarded to keep that
/// behavior. Only the constructor's <c>ISender</c> is guarded, and that is tested by direct
/// construction.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ProductsControllerTests : IDisposable
{
    private const string ProductsUri = "/api/products";
    private const string NameErrorKey = "Name";

    // FluentValidation's NotEmpty message ("'Name' must not be empty.") vs MVC's implicit
    // [Required] message ("The Name field is required."): the only evidence of which layer answered.
    private const string NotEmptyFragment = "must not be empty";
    private const string FrameworkRequiredFragment = "is required";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ProductsControllerTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public ProductsControllerTests()
    {
        _client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
    }

    /// <summary>Gets the rows for <see cref="DeleteAsync_Role_ReturnsExpectedStatus_Test"/>: only the exact role "Administrator" (role names are case-sensitive) allows a delete.</summary>
    public static TheoryData<
        string,
        HttpStatusCode
    > DeleteAsync_Role_ReturnsExpectedStatus_Test_Data =>
        new()
        {
            { ProductRequestMother.AdministratorRole, HttpStatusCode.NoContent },
            { "Support", HttpStatusCode.Forbidden },
            { "administrator", HttpStatusCode.Forbidden },
        };

    /// <summary>Gets the blank names for <see cref="CreateAsync_BlankName_Returns400FromValidator_Test"/>: empty, space, tab, newline.</summary>
    public static TheoryData<string> CreateAsync_BlankName_Returns400FromValidator_Test_Data =>
        new() { string.Empty, " ", "\t", "\n" };

    /// <summary>Disposes the test's <see cref="HttpClient"/> and its backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies constructing the controller without a sender fails fast rather than on first request.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullSender_ThrowsArgumentNullException_Test()
    {
        // Arrange
        const string parameterName = "sender";

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new ProductsController(null!));

        // Assert
        Assert.Equal(parameterName, ex.ParamName);
    }

    /// <summary>Verifies a valid create returns 201 with the created product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_ValidBody_Returns201WithCreatedProduct_Test()
    {
        // Arrange
        var request = ProductRequestMother.Widget();

        // Act
        using var response = await _client.PostAsJsonAsync(
            ProductsUri,
            request,
            CancellationToken.None
        );

        // Assert
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, response.StatusCode),
            () => Assert.Equal(ProductRequestMother.WidgetName, dto.Name),
            () => Assert.Equal(ProductRequestMother.WidgetPrice, dto.Price)
        );
    }

    /// <summary>
    /// Verifies a valid create's Location header resolves to the created product. CreatedAtAction
    /// resolves its target by action name, so the header only works if <c>nameof(GetByIdAsync)</c>
    /// matches the name MVC registered (<c>SuppressAsyncSuffixInActionNames = false</c>).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_ValidBody_LocationHeaderResolvesToProduct_Test()
    {
        // Arrange
        using var created = await _client.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );

        // Act
        using var located = await _client.GetAsync(
            created.Headers.Location,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, located.StatusCode);
    }

    /// <summary>Verifies a create returns the new product's weak ETag, and the body carries the same version.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_ValidBody_ReturnsETagOfVersionOne_Test()
    {
        // Arrange
        var request = ProductRequestMother.Widget();

        // Act
        using var response = await _client.PostAsJsonAsync(
            ProductsUri,
            request,
            CancellationToken.None
        );

        // Assert
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal("W/\"1\"", response.Headers.ETag?.ToString()),
            () => Assert.Equal(1L, dto?.Version.Value)
        );
    }

    /// <summary>Verifies a GET returns the product's current weak ETag.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ReturnsItsETag_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}/{created.Id.Value}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal("W/\"1\"", response.Headers.ETag?.ToString());
    }

    /// <summary>Verifies an invalid create request returns 400 with per-field validation errors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_InvalidBody_Returns400WithPerFieldErrors_Test()
    {
        // Arrange
        var request = ProductRequestMother.InvalidCreate();

        // Act
        using var response = await _client.PostAsJsonAsync(
            ProductsUri,
            request,
            CancellationToken.None
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Contains("Name", body, StringComparison.Ordinal),
            () => Assert.Contains("Price", body, StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Verifies an empty, whitespace, tab, or newline product name passes model binding, reaches
    /// <c>CreateProductValidator</c>'s <c>NotEmpty</c> rule, and comes back as 400 with that
    /// rule's message under the <c>Name</c> key (not the framework's "field is required" message).
    /// </summary>
    /// <param name="name">The blank product name to send.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Theory]
    [MemberData(nameof(CreateAsync_BlankName_Returns400FromValidator_Test_Data))]
    public async Task CreateAsync_BlankName_Returns400FromValidator_Test(string name)
    {
        // Arrange
        var request = new CreateProductRequest(name, ProductRequestMother.WidgetPrice);

        // Act
        using var response = await _client.PostAsJsonAsync(
            ProductsUri,
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        var messages = await ReadNameErrorsAsync(response);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () =>
                Assert.Contains(
                    messages,
                    m => m.Contains(NotEmptyFragment, StringComparison.Ordinal)
                ),
            () =>
                Assert.DoesNotContain(
                    messages,
                    m => m.Contains(FrameworkRequiredFragment, StringComparison.Ordinal)
                )
        );
    }

    /// <summary>
    /// Verifies a JSON <c>null</c> name is rejected with 400 by MVC model validation (the implicit
    /// <c>[Required]</c> on a non-nullable reference type) before the MediatR pipeline, so
    /// <c>CreateProductValidator</c> never sees it; the body carries the framework's message.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task CreateAsync_NullName_Returns400FromModelBinding_Test()
    {
        // Arrange
        var request = new CreateProductRequest(null!, ProductRequestMother.WidgetPrice);

        // Act
        using var response = await _client.PostAsJsonAsync(
            ProductsUri,
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        var messages = await ReadNameErrorsAsync(response);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () =>
                Assert.Contains(
                    messages,
                    m => m.Contains(FrameworkRequiredFragment, StringComparison.Ordinal)
                ),
            () =>
                Assert.DoesNotContain(
                    messages,
                    m => m.Contains(NotEmptyFragment, StringComparison.Ordinal)
                )
        );
    }

    /// <summary>Verifies a request whose body is the JSON literal <c>null</c> is rejected with 400 rather than reaching the handler.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task CreateAsync_NullBody_Returns400_Test()
    {
        // Arrange
        using var content = new StringContent("null", Encoding.UTF8, "application/json");

        // Act
        using var response = await _client.PostAsync(ProductsUri, content, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_MissingProduct_Returns404_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{ProductRequestMother.UnknownId}";

        // Act
        using var response = await _client.GetAsync(uri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies a missing product's 404 body is an RFC 7807 problem document carrying the stable NOT_FOUND code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_MissingProduct_ReturnsProblemJsonWithNotFoundCode_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{ProductRequestMother.UnknownId}";

        // Act
        using var response = await _client.GetAsync(uri, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var document = JsonDocument.Parse(body);

        // Assert
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Multiple(
            () => Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32()),
            () => Assert.Equal("NOT_FOUND", document.RootElement.GetProperty("code").GetString()),
            () =>
                Assert.Contains(
                    ProductRequestMother.UnknownId.ToString(),
                    document.RootElement.GetProperty("detail").GetString(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    /// <summary>Verifies an empty-guid id fails validation and returns 400 rather than 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_EmptyGuid_Returns400_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{Guid.Empty}";

        // Act
        using var response = await _client.GetAsync(uri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies an administrator deleting with an empty-guid id fails validation and returns 400 rather than 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_EmptyGuid_Returns400_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{Guid.Empty}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            uri,
            roles: [ProductRequestMother.AdministratorRole]
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies an existing product returns 200 with that product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task GetByIdAsync_ExistingProduct_Returns200WithProduct_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}/{created.Id.Value}",
            CancellationToken.None
        );

        // Assert
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal(created.Id, dto.Id),
            () => Assert.Equal(ProductRequestMother.WidgetName, dto.Name),
            () => Assert.Equal(ProductRequestMother.WidgetPrice, dto.Price)
        );
    }

    /// <summary>Verifies paging only returns products from this test's own isolated database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_OneCreatedProduct_ReturnsOnlyThatProduct_Test()
    {
        // Arrange
        await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageNumber=1&pageSize=10",
            CancellationToken.None
        );

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(
            CancellationToken.None
        );
        Assert.NotNull(page);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Single(page.Items),
            () => Assert.Equal(ProductRequestMother.WidgetName, page.Items[0].Name)
        );
    }

    /// <summary>Verifies products created out of alphabetical order are listed ordered by name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task GetPagedAsync_ProductsCreatedOutOfOrder_ReturnsOrderedByName_Test()
    {
        // Arrange
        await CreateProductAsync(ProductRequestMother.Named("Bravo"));
        await CreateProductAsync(ProductRequestMother.Named("Alpha"));

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageNumber=1&pageSize=10",
            CancellationToken.None
        );

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(
            CancellationToken.None
        );
        Assert.NotNull(page);
        Assert.Collection(
            page.Items,
            first => Assert.Equal("Alpha", first.Name),
            second => Assert.Equal("Bravo", second.Name)
        );
    }

    /// <summary>Verifies out-of-range paging parameters return 400 rather than 500.</summary>
    /// <param name="query">The query string carrying invalid paging parameters.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("pageNumber=0&pageSize=10")]
    [InlineData("pageNumber=1&pageSize=0")]
    [InlineData("pageNumber=1&pageSize=101")]
    public async Task GetPagedAsync_OutOfRangePaging_Returns400_Test(string query)
    {
        // Arrange
        var uri = $"{ProductsUri}?{query}";

        // Act
        using var response = await _client.GetAsync(uri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies updating a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_MissingProduct_Returns404_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{ProductRequestMother.UnknownId}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: ProductVersion.Initial.ToETag()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an owner's update with an invalid body returns 400 with per-field validation errors (authorization runs before validation, so the caller must own the product).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_OwnerWithInvalidBody_Returns400WithPerFieldErrors_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            $"{ProductsUri}/{created.Id.Value}",
            ProductRequestMother.InvalidUpdate(),
            ProductRequestMother.OwnerId,
            ifMatch: created.Version.ToETag()
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Contains("Name", body, StringComparison.Ordinal),
            () => Assert.Contains("Price", body, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies an owner's update returns 204 and its changes are visible to a subsequent GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_Owner_Returns204AndPersistsChanges_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: created.Version.ToETag()
        );

        // Assert
        var fetched = await _client.GetFromJsonAsync<ProductDto>(uri, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode),
            () => Assert.Equal(ProductRequestMother.WidgetProName, fetched.Name),
            () => Assert.Equal(ProductRequestMother.WidgetProPrice, fetched.Price)
        );
    }

    /// <summary>Verifies an update without an If-Match header is refused with 428 Precondition Required.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_NoIfMatchHeader_Returns428_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            $"{ProductsUri}/{created.Id.Value}",
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId
        );

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    /// <summary>Verifies a malformed If-Match header is a 400 validation problem naming the header.</summary>
    /// <param name="malformed">The malformed If-Match value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("not-an-etag")]
    [InlineData("\"1\"")]
    [InlineData("W/\"abc\"")]
    [InlineData("*")]
    public async Task UpdateAsync_MalformedIfMatch_Returns400NamingTheHeader_Test(string malformed)
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            $"{ProductsUri}/{created.Id.Value}",
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: malformed
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Contains("If-Match", body, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies an update carrying a stale ETag is refused with 412 and changes nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_StaleIfMatch_Returns412AndLeavesProductUntouched_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";
        using var firstUpdate = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: "W/\"1\""
        );

        // Act
        using var staleUpdate = await SendAsync(
            HttpMethod.Put,
            uri,
            new UpdateProductRequest("Stale writer", 1m),
            ProductRequestMother.OwnerId,
            ifMatch: "W/\"1\""
        );

        // Assert
        var fetched = await _client.GetFromJsonAsync<ProductDto>(uri, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, firstUpdate.StatusCode),
            () => Assert.Equal(HttpStatusCode.PreconditionFailed, staleUpdate.StatusCode),
            () => Assert.Equal(ProductRequestMother.WidgetProName, fetched.Name)
        );
    }

    /// <summary>Verifies a matching If-Match yields 204 with the new ETag, and a subsequent GET reports that same ETag.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_MatchingIfMatch_Returns204WithNewETagThatGetAlsoReports_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: "W/\"1\""
        );

        // Assert
        using var afterUpdate = await _client.GetAsync(uri, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode),
            () => Assert.Equal("W/\"2\"", response.Headers.ETag?.ToString()),
            () => Assert.Equal("W/\"2\"", afterUpdate.Headers.ETag?.ToString())
        );
    }

    /// <summary>Verifies two concurrent updates carrying the same ETag resolve to exactly one 204 and one 412 — never two winners, never a 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_TwoRacingUpdatesWithSameETag_ExactlyOneSucceedsAndOneGets412_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";
        var etag = created.Version.ToETag();

        // Act
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, 2)
                .Select(index =>
                    SendAsync(
                        HttpMethod.Put,
                        uri,
                        new UpdateProductRequest($"Writer {index}", 1m),
                        ProductRequestMother.OwnerId,
                        ifMatch: etag
                    )
                )
        );

        // Assert
        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.PreconditionFailed], statuses);
    }

    /// <summary>Verifies a non-owner's update returns 403 and leaves the product untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_NonOwner_Returns403AndLeavesProductUntouched_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OtherCallerId,
            ifMatch: created.Version.ToETag()
        );

        // Assert
        var fetched = await _client.GetFromJsonAsync<ProductDto>(uri, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal(ProductRequestMother.WidgetName, fetched.Name),
            () => Assert.Equal(ProductRequestMother.WidgetPrice, fetched.Price)
        );
    }

    /// <summary>Verifies an update by an authenticated caller whose token has no subject returns 403, since such a caller never matches any product's owner.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_CallerWithoutSubject_Returns403_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            $"{ProductsUri}/{created.Id.Value}",
            ProductRequestMother.WidgetPro(),
            callerId: string.Empty,
            ifMatch: created.Version.ToETag()
        );

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies creating as an authenticated caller whose token has no subject returns a 403 problem
    /// (the <c>NotAuthorized</c> outcome) and creates nothing: there is no identity to record as the owner.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_CallerWithoutSubject_Returns403AndCreatesNothing_Test()
    {
        // Act
        using var response = await SendAsync(
            HttpMethod.Post,
            ProductsUri,
            ProductRequestMother.Widget(),
            callerId: string.Empty
        );

        // Assert
        using var list = await _client.GetAsync(ProductsUri, CancellationToken.None);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(
            CancellationToken.None
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () =>
                Assert.Equal(
                    "application/problem+json",
                    response.Content.Headers.ContentType?.MediaType
                ),
            () => Assert.Empty(page!.Items)
        );
    }

    /// <summary>Verifies a created product is owned by the caller who created it: that caller may update it, the default caller may not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_AuthenticatedCaller_RecordsCallerAsOwner_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var byOwner = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: created.Version.ToETag()
        );
        using var byOther = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ifMatch: created.Version.ToETag()
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode),
            () => Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode)
        );
    }

    /// <summary>Verifies an administrator's delete returns 204 and the product subsequently returns 404 from GetById.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_Administrator_Returns204AndProductIsGone_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            uri,
            roles: [ProductRequestMother.AdministratorRole]
        );

        // Assert
        using var afterDelete = await _client.GetAsync(uri, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode),
            () => Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode)
        );
    }

    /// <summary>Verifies a delete carrying a stale ETag is refused with 412 and the product survives.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_StaleIfMatch_Returns412AndProductSurvives_Test()
    {
        // Arrange
        var created = await CreateProductAsync(
            ProductRequestMother.Widget(),
            ProductRequestMother.OwnerId
        );
        var uri = $"{ProductsUri}/{created.Id.Value}";
        using var update = await SendAsync(
            HttpMethod.Put,
            uri,
            ProductRequestMother.WidgetPro(),
            ProductRequestMother.OwnerId,
            ifMatch: created.Version.ToETag()
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            uri,
            roles: [ProductRequestMother.AdministratorRole],
            ifMatch: created.Version.ToETag()
        );

        // Assert
        using var afterDelete = await _client.GetAsync(uri, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode)
        );
    }

    /// <summary>Verifies a delete carrying the current ETag proceeds.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_MatchingIfMatch_Returns204_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"{ProductsUri}/{created.Id.Value}",
            roles: [ProductRequestMother.AdministratorRole],
            ifMatch: created.Version.ToETag()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Verifies a malformed If-Match on a delete is a 400 rather than being silently ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_MalformedIfMatch_Returns400_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"{ProductsUri}/{created.Id.Value}",
            roles: [ProductRequestMother.AdministratorRole],
            ifMatch: "garbage"
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies an administrator deleting a missing product returns 404.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_MissingProduct_Returns404_Test()
    {
        // Arrange
        var uri = $"{ProductsUri}/{ProductRequestMother.UnknownId}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            uri,
            roles: [ProductRequestMother.AdministratorRole]
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies a delete by a caller without the Administrator role returns 403, and the product is left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DeleteAsync_NonAdministrator_Returns403AndLeavesProductUntouched_Test()
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());
        var uri = $"{ProductsUri}/{created.Id.Value}";

        // Act
        using var response = await _client.DeleteAsync(uri, CancellationToken.None);

        // Assert
        using var afterDelete = await _client.GetAsync(uri, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode)
        );
    }

    /// <summary>Verifies only the exact Administrator role grants administrator rights.</summary>
    /// <param name="role">The role the caller holds.</param>
    /// <param name="expectedStatus">The status the delete is expected to return.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Theory]
    [MemberData(nameof(DeleteAsync_Role_ReturnsExpectedStatus_Test_Data))]
    public async Task DeleteAsync_Role_ReturnsExpectedStatus_Test(
        string role,
        HttpStatusCode expectedStatus
    )
    {
        // Arrange
        var created = await CreateProductAsync(ProductRequestMother.Widget());

        // Act
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"{ProductsUri}/{created.Id.Value}",
            roles: [role]
        );

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    /// <summary>Reads the RFC 7807 validation problem body and returns the messages listed under the <c>Name</c> key.</summary>
    /// <param name="response">The 400 response to read.</param>
    /// <returns>The error messages for the <c>Name</c> property; empty when the key is absent.</returns>
    private static async Task<IReadOnlyList<string>> ReadNameErrorsAsync(
        HttpResponseMessage response
    )
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        if (
            !document.RootElement.TryGetProperty("errors", out var errors)
            || !errors.TryGetProperty(NameErrorKey, out var name)
        )
        {
            return [];
        }

        List<string> messages = [];
        for (var index = 0; index < name.GetArrayLength(); index++)
        {
            messages.Add(name[index].GetString() ?? string.Empty);
        }

        return messages;
    }

    /// <summary>Creates a product through the API and returns the created representation.</summary>
    /// <param name="request">The create request to send.</param>
    /// <param name="callerId">The caller to create the product as (it becomes the owner), or <see langword="null"/> for the client's default caller.</param>
    /// <returns>The created product.</returns>
    private async Task<ProductDto> CreateProductAsync(
        CreateProductRequest request,
        string? callerId = null
    )
    {
        using var response = await SendAsync(HttpMethod.Post, ProductsUri, request, callerId);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        return dto!;
    }

    /// <summary>Sends a request from the given caller, or from the client's default caller when neither an id nor roles are given.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="body">The JSON body, or <see langword="null"/> for none.</param>
    /// <param name="callerId">The caller's id, or <see langword="null"/> for the client's default caller (an empty string is an authenticated caller with no subject).</param>
    /// <param name="roles">The roles the caller holds, or <see langword="null"/> for none; a caller with roles but no <paramref name="callerId"/> is the admin caller.</param>
    /// <param name="ifMatch">The raw <c>If-Match</c> header value, or <see langword="null"/> to omit it.</param>
    /// <returns>The response to the request.</returns>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        object? body = null,
        string? callerId = null,
        string[]? roles = null,
        string? ifMatch = null
    )
    {
        using var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (roles is not null)
        {
            request.AsUser(callerId ?? ProductRequestMother.AdminCallerId, roles);
        }
        else if (callerId is not null)
        {
            request.AsUser(callerId);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _client.SendAsync(request, CancellationToken.None);
    }
}

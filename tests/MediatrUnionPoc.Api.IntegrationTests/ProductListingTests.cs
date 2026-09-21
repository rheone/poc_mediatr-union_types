using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the list endpoint's query-string contract over real HTTP against the real host and
/// real SQLite: filter and sort binding, per-field 400s, the paging metadata in the body, and the
/// <c>X-Total-Count</c> and <c>Link</c> headers.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductListingTests : IDisposable
{
    private const string ProductsUri = ApiRoutes.Products;
    private const string Origin = "http://localhost";
    private const string LinkHeader = "Link";
    private const string TotalCountHeader = "X-Total-Count";
    private const string ProblemJson = "application/problem+json";

    private static readonly DateTimeOffset ClockStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _clock = new(ClockStart);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    /// <summary>Initializes the test host with a clock the test controls.</summary>
    public ProductListingTests()
    {
        _factory = new ProductsApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            })
        );
        _client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies the <c>nameContains</c> query parameter filters case-insensitively and the total counts only matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NameContains_ListsOnlyMatchingProducts_Test()
    {
        // Arrange
        await CreateAsync("Blue Widget");
        await CreateAsync("Red WIDGET");
        await CreateAsync("Anvil");

        // Act
        var page = await ListAsync("nameContains=widget");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Blue Widget", "Red WIDGET"], page.Items.Select(p => p.Name)),
            () => Assert.Equal(2, page.TotalCount)
        );
    }

    /// <summary>Verifies <c>minPrice</c>, <c>maxPrice</c> and <c>ownerId</c> each bind from the query string and combine.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_PriceBoundsAndOwner_FilterTogether_Test()
    {
        // Arrange
        await CreateAsync("Cheap Mine", 5m, "alice");
        await CreateAsync("Fair Mine", 15m, "alice");
        await CreateAsync("Fair Theirs", 15m, "bob");
        await CreateAsync("Dear Mine", 500m, "alice");

        // Act
        var priced = await ListAsync("minPrice=10&maxPrice=100");
        var owned = await ListAsync("ownerId=alice&maxPrice=100.00");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Fair Mine", "Fair Theirs"], priced.Items.Select(p => p.Name)),
            () => Assert.Equal(["Cheap Mine", "Fair Mine"], owned.Items.Select(p => p.Name))
        );
    }

    /// <summary>Verifies <c>sort=name,-price</c> binds as a comma-separated list, ties on the first key ordered by the second (descending).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_SortNameThenDescendingPrice_OrdersByBothKeys_Test()
    {
        // Arrange
        await CreateAsync("Bravo", 10m);
        await CreateAsync("Alpha", 30m);
        await CreateAsync("Charlie", 20m);

        // Act
        var byPriceDescending = await ListAsync("sort=-price");
        var byNameThenPrice = await ListAsync("sort=name,-price");

        // Assert
        Assert.Multiple(
            () =>
                Assert.Equal(
                    ["Alpha", "Charlie", "Bravo"],
                    byPriceDescending.Items.Select(p => p.Name)
                ),
            () =>
                Assert.Equal(
                    ["Alpha", "Bravo", "Charlie"],
                    byNameThenPrice.Items.Select(p => p.Name)
                ),
            () =>
                Assert.Equal(
                    [
                        new ProductSort(ProductSortField.Name, SortDirection.Ascending),
                        new ProductSort(ProductSortField.Price, SortDirection.Descending),
                    ],
                    byNameThenPrice.Sort
                )
        );
    }

    /// <summary>Verifies each product's creation instant comes from the clock at creation time, appears in the body, and drives <c>sort=-createdAt</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_SortNewestFirst_OrdersByCreationInstantAndExposesIt_Test()
    {
        // Arrange
        _clock.SetUtcNow(new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero));
        await CreateAsync("First");
        _clock.SetUtcNow(new DateTimeOffset(2026, 2, 3, 9, 0, 0, TimeSpan.Zero));
        await CreateAsync("Third");
        _clock.SetUtcNow(new DateTimeOffset(2026, 2, 2, 9, 0, 0, TimeSpan.Zero));
        await CreateAsync("Second");

        // Act
        var page = await ListAsync("sort=-createdAt");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Third", "Second", "First"], page.Items.Select(p => p.Name)),
            () =>
                Assert.Equal(
                    new DateTimeOffset(2026, 2, 3, 9, 0, 0, TimeSpan.Zero),
                    page.Items[0].CreatedAt
                )
        );
    }

    /// <summary>Gets the rows for <see cref="GetPagedAsync_InvalidQuery_Returns400WithErrorPerOffendingField_Test"/>: the query string and the fields expected in the <c>errors</c> member.</summary>
    public static TheoryData<
        string,
        string[]
    > GetPagedAsync_InvalidQuery_Returns400WithErrorPerOffendingField_Test_Data =>
        new()
        {
            { "sort=weight", ["Sort"] },
            { "sort=name,,price", ["Sort"] },
            { "minPrice=20&maxPrice=10", ["MaxPrice"] },
            { "minPrice=-1", ["MinPrice"] },
            { "pageNumber=0", ["PageNumber"] },
            { "pageSize=101", ["PageSize"] },
            {
                "pageNumber=-3&pageSize=0&sort=nope&minPrice=-5",
                ["MinPrice", "PageNumber", "PageSize", "Sort"]
            },
        };

    /// <summary>Verifies bad input answers 400 with a validation problem whose <c>errors</c> has exactly one entry per offending query field.</summary>
    /// <param name="query">The invalid query string.</param>
    /// <param name="expectedFields">The error keys expected, sorted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(GetPagedAsync_InvalidQuery_Returns400WithErrorPerOffendingField_Test_Data))]
    public async Task GetPagedAsync_InvalidQuery_Returns400WithErrorPerOffendingField_Test(
        string query,
        string[] expectedFields
    )
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?{query}",
            CancellationToken.None
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(body);
        var fields = ErrorKeys(json.RootElement);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(expectedFields, fields)
        );
    }

    /// <summary>Verifies a value that is not a number (rejected by model binding, before any validator) also answers 400 naming its field.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NonNumericPrice_Returns400NamingTheField_Test()
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?minPrice=abc",
            CancellationToken.None
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(body);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Contains("MinPrice", ErrorKeys(json.RootElement))
        );
    }

    /// <summary>Verifies the message for a bad sort names the field and lists what is allowed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_UnknownSortField_ErrorMessageListsAllowedFields_Test()
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?sort=weight",
            CancellationToken.None
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("errors").GetProperty("Sort")[0].GetString();
        Assert.Equal(
            "'weight' is not a sortable field; use one of: name, price, createdAt.",
            message
        );
    }

    /// <summary>Verifies the body carries the applied sort and every paging value under its wire name, worked by hand for 7 products at size 2, page 2 (4 pages).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_MiddlePage_BodyCarriesSortAndPagingMetadata_Test()
    {
        // Arrange
        for (var i = 1; i <= 7; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageNumber=2&pageSize=2&sort=-name",
            CancellationToken.None
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Multiple(
            () =>
                Assert.Equal(
                    "Item 5",
                    root.GetProperty("items")[0].GetProperty("name").GetString()
                ),
            () =>
                Assert.Equal(
                    "Item 4",
                    root.GetProperty("items")[1].GetProperty("name").GetString()
                ),
            () => Assert.Equal(2, root.GetProperty("pageNumber").GetInt32()),
            () => Assert.Equal(2, root.GetProperty("pageSize").GetInt32()),
            () => Assert.Equal(7, root.GetProperty("totalCount").GetInt32()),
            () => Assert.Equal(4, root.GetProperty("totalPages").GetInt32()),
            () => Assert.Equal(1, root.GetProperty("firstPage").GetInt32()),
            () => Assert.Equal(4, root.GetProperty("lastPage").GetInt32()),
            () => Assert.Equal(3, root.GetProperty("nextPage").GetInt32()),
            () => Assert.Equal(1, root.GetProperty("previousPage").GetInt32()),
            () =>
                Assert.Equal("name", root.GetProperty("sort")[0].GetProperty("field").GetString()),
            () =>
                Assert.Equal(
                    "descending",
                    root.GetProperty("sort")[0].GetProperty("direction").GetString()
                )
        );
    }

    /// <summary>Verifies a page past the end is still 200: no items, the true total, and a previous page that points back at the real last page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_PagePastTheEnd_Is200WithNoItemsAndCorrectMetadata_Test()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        var page = await ListAsync("pageNumber=9&pageSize=2");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(page.Items),
            () => Assert.Equal(5, page.TotalCount),
            () => Assert.Equal(3, page.LastPage),
            () => Assert.Null(page.NextPage),
            () => Assert.Equal(3, page.PreviousPage)
        );
    }

    /// <summary>Verifies omitting every parameter lists the first page of 10 in name order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NoQueryString_UsesFirstPageOfTenByName_Test()
    {
        // Arrange
        for (var i = 1; i <= 12; i++)
        {
            await CreateAsync($"Item {i:00}");
        }

        // Act
        using var response = await _client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(
            CancellationToken.None
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal(10, page!.Items.Count),
            () => Assert.Equal(1, page!.PageNumber),
            () => Assert.Equal(10, page!.PageSize),
            () => Assert.Equal("Item 01", page!.Items[0].Name)
        );
    }

    /// <summary>Verifies <c>X-Total-Count</c> is the number of products matching the filters, not the table size, and stays correct past the last page.</summary>
    /// <param name="query">The query string.</param>
    /// <param name="expectedTotal">The expected header value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("pageSize=2", "5")]
    [InlineData("nameContains=widget", "3")]
    [InlineData("nameContains=widget&pageNumber=9", "3")]
    [InlineData("nameContains=nothing-like-this", "0")]
    public async Task GetPagedAsync_Success_SetsTotalCountHeaderToMatchingProducts_Test(
        string query,
        string expectedTotal
    )
    {
        // Arrange
        foreach (var name in new[] { "Widget A", "Widget B", "Widget C", "Anvil", "Bolt" })
        {
            await CreateAsync(name);
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?{query}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expectedTotal, Assert.Single(response.Headers.GetValues(TotalCountHeader)));
    }

    /// <summary>Verifies a middle page links to first, prev, next and last, preserving every other query parameter and replacing only the page number in place.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_MiddlePage_LinkHeaderHasFirstPrevNextLast_Test()
    {
        // Arrange
        for (var i = 1; i <= 7; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?nameContains=item&pageSize=2&pageNumber=2&sort=name,-price",
            CancellationToken.None
        );

        // Assert
        const string Base =
            $"{Origin}{ApiRoutes.Products}?nameContains=item&pageSize=2&pageNumber=";
        const string Rest = "&sort=name,-price";
        Assert.Equal(
            $"<{Base}1{Rest}>; rel=\"first\", <{Base}1{Rest}>; rel=\"prev\", <{Base}3{Rest}>; rel=\"next\", <{Base}4{Rest}>; rel=\"last\"",
            Assert.Single(response.Headers.GetValues(LinkHeader))
        );
    }

    /// <summary>Verifies the first page has no prev link and the last page has no next link.</summary>
    /// <param name="pageNumber">The requested page of 3 (5 products at size 2).</param>
    /// <param name="expectedRels">The rels expected, in order.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(1, "first,next,last")]
    [InlineData(3, "first,prev,last")]
    public async Task GetPagedAsync_EdgePage_OmitsPrevOnFirstAndNextOnLast_Test(
        int pageNumber,
        string expectedRels
    )
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageSize=2&pageNumber={pageNumber}",
            CancellationToken.None
        );

        // Assert
        var link = Assert.Single(response.Headers.GetValues(LinkHeader));
        var rels = link.Split(", ")
            .Select(part => part[(part.IndexOf("rel=\"", StringComparison.Ordinal) + 5)..^1]);
        Assert.Equal(expectedRels.Split(','), rels);
    }

    /// <summary>Verifies a request that left out the page number gets links that add it, after the parameters that were sent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NoPageNumberSent_LinksAppendIt_Test()
    {
        // Arrange
        for (var i = 1; i <= 3; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageSize=2",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(
            $"<{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=1>; rel=\"first\", <{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=2>; rel=\"next\", <{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=2>; rel=\"last\"",
            Assert.Single(response.Headers.GetValues(LinkHeader))
        );
    }

    /// <summary>Verifies a page past the end links back to the real last page and offers no next.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_PagePastTheEnd_LinkHeaderPointsBackAtRealLastPage_Test()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync($"Item {i}");
        }

        // Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageSize=2&pageNumber=9",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(
            $"<{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=1>; rel=\"first\", <{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=3>; rel=\"prev\", <{Origin}{ApiRoutes.Products}?pageSize=2&pageNumber=3>; rel=\"last\"",
            Assert.Single(response.Headers.GetValues(LinkHeader))
        );
    }

    /// <summary>Verifies an empty listing still has a Link header: one page, first and last only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NoMatches_LinkHeaderHasOnlyFirstAndLast_Test()
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?nameContains=zzz",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(
            $"<{Origin}{ApiRoutes.Products}?nameContains=zzz&pageNumber=1>; rel=\"first\", <{Origin}{ApiRoutes.Products}?nameContains=zzz&pageNumber=1>; rel=\"last\"",
            Assert.Single(response.Headers.GetValues(LinkHeader))
        );
    }

    /// <summary>Verifies a rejected request carries neither paging header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_InvalidQuery_HasNoPagingHeaders_Test()
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?pageSize=0",
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.False(response.Headers.Contains(LinkHeader)),
            () => Assert.False(response.Headers.Contains(TotalCountHeader))
        );
    }

    /// <summary>Verifies characters that need escaping in a query value (ampersand, plus, space) survive into the link URLs still escaped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_QueryValueNeedingEscapes_LinksKeepItEscaped_Test()
    {
        // Arrange / Act
        using var response = await _client.GetAsync(
            $"{ProductsUri}?nameContains=a%26b%2Bc%20d",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(
            $"<{Origin}{ApiRoutes.Products}?nameContains=a%26b%2Bc%20d&pageNumber=1>; rel=\"first\", <{Origin}{ApiRoutes.Products}?nameContains=a%26b%2Bc%20d&pageNumber=1>; rel=\"last\"",
            Assert.Single(response.Headers.GetValues(LinkHeader))
        );
    }

    /// <summary>Verifies the link builder rejects a null request rather than failing later.</summary>
    [Fact]
    public void ToLinkHeader_NullRequest_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var page = new PagedResult<string>([], 1, 10, 0, ProductSort.Default);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => page.ToLinkHeader(null!));

        // Assert
        Assert.Equal("request", ex.ParamName);
    }

    /// <summary>Verifies setting paging headers rejects a null page rather than failing later.</summary>
    [Fact]
    public void SetPagingHeaders_NullPage_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var response = new Microsoft.AspNetCore.Http.DefaultHttpContext().Response;

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            response.SetPagingHeaders<string>(null!);
        });

        // Assert
        Assert.Equal("page", ex.ParamName);
    }

    private static string[] ErrorKeys(JsonElement problem) =>
        [
            .. problem
                .GetProperty("errors")
                .Deserialize<Dictionary<string, string[]>>()!
                .Keys.Order(StringComparer.Ordinal),
        ];

    private async Task<ProductDto> CreateAsync(
        string name,
        decimal price = 1m,
        string? ownerId = null
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ProductsUri)
        {
            Content = JsonContent.Create(new CreateProductRequest(name, price)),
        };
        if (ownerId is not null)
        {
            request.AsUser(ownerId);
        }

        using var response = await _client.SendAsync(request, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None))!;
    }

    private async Task<PagedResult<ProductDto>> ListAsync(string query)
    {
        using var response = await _client.GetAsync(
            $"{ProductsUri}?{query}",
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (
            await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(
                CancellationToken.None
            )
        )!;
    }
}

# Listing products: filtering, sorting and paging

Part of the [documentation](index.md).

`GET /api/v1/products` returns one page of the products that match optional filters, in a requested
order. The request is bound from the query string, and every parameter is optional:

| Parameter      | Meaning                                                                                       |
| -------------- | --------------------------------------------------------------------------------------------- |
| `nameContains` | Name contains this text, ignoring case and surrounding whitespace (max 200 characters)          |
| `minPrice`     | Price at least this (inclusive, not negative)                                                  |
| `maxPrice`     | Price at most this (inclusive, not negative, not below `minPrice`)                             |
| `ownerId`      | Owned by exactly this caller id (the `sub` of the token used at creation; max 200 characters)   |
| `sort`         | Comma-separated keys in priority order; `name`, `price` or `createdAt`, `-` prefix for descending (`name,-price`). Default `name` |
| `pageNumber`   | 1-based page number (default 1)                                                                |
| `pageSize`     | 1 to 100 (default 10)                                                                          |

Filters combine with AND. Names bind case-insensitively, so `PageNumber=2` works too.

```bash
curl -i 'localhost:5233/api/v1/products?nameContains=widget&minPrice=5&sort=name,-price&pageNumber=2&pageSize=2'
```

**Database-agnostic by construction.** Domain and Application never see `IQueryable` or EF Core.
The Domain defines what a caller may ask for —
[`ProductCriteria`](../src/MediatrUnionPoc.Domain/ProductCriteria.cs) (the filters) and
[`ProductSort`](../src/MediatrUnionPoc.Domain/ProductSort.cs) (a `ProductSortField` of `Name`,
`Price` or `CreatedAt`, plus a `SortDirection`) — and `IProductRepository.GetPagedAsync(pageNumber,
pageSize, criteria, sort, ct)` takes exactly those. Sorting is an **enum allowlist**, so a caller
can never name a property outside it; the text form (`name,-price`) is parsed by
[`ProductSortParser`](../src/MediatrUnionPoc.Application/Features/Products/GetPaged/ProductSortParser.cs)
in Application. Only `ProductRepository` in Infrastructure turns criteria and sort into a query:

- `nameContains` reuses the duplicate-name rule's normalisation (`ProductNames.Normalize`) against
  the stored `NormalizedName` column, so the match is case-insensitive without any provider-specific
  collation or function.
- Every sort **ends in an implicit `Id` tiebreaker**, so products that tie on every requested key
  still have one total order and consecutive pages never skip or repeat a row. Asking for no sort
  means `name` ascending.
- `Product.CreatedAt` is set by the Application layer from an injectable `TimeProvider` (registered
  as `TimeProvider.System` unless the host registered its own), never by the Domain reading a clock.
  SQLite refuses `ORDER BY` on the text it would store for a `DateTimeOffset`, so Infrastructure
  stores it through a hand-written `UtcTicksValueConverter` (UTC ticks in a `long`): it sorts by
  instant on any provider, at the cost of the original offset (a value read back is the same
  instant at offset zero). `ProductDto` exposes it as `createdAt`.
- Comparing and ordering `Money` works because it defines the relational operators (Vogen generates
  none); EF Core translates them against the decimal column.

**Bad input is a validation problem, per field.** `GetPagedProductsResult` declares a real
`ValidationErrors` case, so `GetPagedProductsValidator`'s failures reach the client as a `400`
`ValidationProblemDetails` whose `errors` names each offending field once per problem
(`PageNumber`, `PageSize`, `MinPrice`, `MaxPrice`, `NameContains`, `OwnerId`, `Sort`). An unknown or
duplicated sort field, an empty key, a minimum above the maximum, a page below 1 and a page size
outside 1 to 100 all end up there; a value that is not a number at all (`minPrice=abc`) is rejected
by model binding with the same shape.

**The response describes where you are.** The body is `PagedResult<ProductDto>`:

```json
{
  "items": [ { "id": "…", "name": "Widget", "price": 9.99, "version": 1, "createdAt": "2026-03-01T09:30:00+00:00" } ],
  "pageNumber": 2, "pageSize": 2, "totalCount": 5, "totalPages": 3,
  "firstPage": 1, "lastPage": 3, "nextPage": 3, "previousPage": 1,
  "sort": [ { "field": "name", "direction": "ascending" } ]
}
```

`totalCount` counts the products matching the filters, not the table. Every derived value
(`totalPages`, `firstPage`, `lastPage`, `nextPage`, `previousPage`) is computed in one place — as
members of the Domain's `PagedResult<T>` — so the body and the headers cannot disagree. An empty
result still has one (empty) page; a page past the end is a `200` with empty `items`, correct
metadata, no `nextPage`, and a `previousPage` pointing at the real last page.

The `200` also carries two headers, built by extension members in
[`PagingHttpExtensions`](../src/MediatrUnionPoc.Api/Http/PagingHttpExtensions.cs) so a client can page
without reading the body:

- `X-Total-Count`: the same number as `totalCount`.
- `Link` (RFC 8288) with `rel` `first`, `prev`, `next` and `last`, omitting `prev` on the first page
  and `next` on the last. Each URL is the canonical versioned one (`/api/v1/products`, even when the
  request used the unversioned alias) with every query parameter of the request kept in
  its original order and only `pageNumber` replaced (or appended if it was not sent). For the request
  above (5 matching products, size 2, page 2):

  ```text
  Link: <http://localhost:5233/api/v1/products?nameContains=widget&minPrice=5&sort=name,-price&pageNumber=1&pageSize=2>; rel="first", <…pageNumber=1…>; rel="prev", <…pageNumber=3…>; rel="next", <…pageNumber=3…>; rel="last"
  ```

The OpenAPI document declares the query parameters (described from the XML docs on
`ListProductsRequest`), the two response headers (`PagingResponseHeaderTransformer`), the `400`
validation problem, and an example paged body.

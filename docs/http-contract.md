# The HTTP contract: every endpoint and outcome

Part of the [documentation](index.md).

**Contents**

- [The HTTP contract: every endpoint and outcome](#the-http-contract-every-endpoint-and-outcome)
  - [API versioning](#api-versioning)

This is the complete mapping the controller's `switch` arms and `ProducesResponseType` attributes
implement. Every non-2xx response is `application/problem+json` (`ValidationProblemDetails`, with an
`errors` member, for validation), and **every response** of any status carries an `X-Trace-Id`
header; problem bodies also carry `traceId` (see
[Trace id and unhandled exceptions](logging-and-errors.md#trace-id-and-unhandled-exceptions)).

**Authentication.** Every product endpoint requires a valid bearer token (a fallback authorization
policy makes the whole host secure by default). A missing, expired, wrongly signed or wrongly
addressed token is `401` (with a `WWW-Authenticate: Bearer` challenge), on every verb and before any
row below applies; the table lists only what a valid caller can get. The middleware's `401` and `403`
are problem bodies with a `traceId` like every other failure. Only `/health/live`, `/health/ready`
and, in Development, `/openapi/v1.json` and `/scalar` are anonymous.

**Rate limiting.** Every endpoint of the table below (all but the health probes and the Development documents)
can also answer `429 Too Many Requests` once the caller has spent its budget: a problem body with `code`
`RATE_LIMITED`, the `traceId` and a `Retry-After` header in whole seconds. It comes from the rate limiter,
after authentication and before authorization, so it is not a union case and is not repeated per row; see
[Rate limiting](operations.md#rate-limiting-a-budget-per-caller).

**Request timeouts.** Likewise every endpoint of the table (again all but the health probes and the Development
documents) can answer `504 Gateway Timeout` when the request outlives its deadline: a problem body with `code`
`REQUEST_TIMEOUT` and the `traceId`. It comes from the timeout middleware, not from a union, and is not repeated
per row; see [Request timeouts](operations.md#request-timeouts-a-deadline-per-request).

Request headers the API reads:

| Header | Used by | Meaning |
| --- | --- | --- |
| `Authorization: Bearer <jwt>` | every product endpoint | The caller's identity: its `sub` becomes the new product's owner on `POST` and must equal the owner on `PUT`/`PATCH`; a `role` of `Administrator` is required on `DELETE` |
| `If-Match: W/"n"` | `PUT`, `PATCH` (required); `DELETE` (optional) | The `ETag` of the version being changed |
| `Content-Type: application/merge-patch+json` | `PATCH` | Required for `PATCH`; anything else is `415` |
| `X-Forwarded-For` | every endpoint, **only** from a configured trusted proxy | The client address a reverse proxy reports; ignored from anyone else. See [Behind a reverse proxy](operations.md#behind-a-reverse-proxy) |

| Endpoint | Union case | Status | Notes |
| --- | --- | --- | --- |
| `POST /api/v1/products` | `ProductDto` | `201` | `ETag: W/"1"`, `Location` header (the versioned URL of the new product), product in the body |
| | `ValidationErrors` | `400` | Per-field `errors` |
| | `NotAuthorized` | `403` | The token is valid but has no `sub` claim, so there is no identity to own the product; answered before anything is sent to MediatR |
| | `Conflict` | `409` | Another product has an equivalent name |
| | `Error` | `500` | Only a commit that cannot fail this way (`COMMIT_CONCURRENCY_CONFLICT`) |
| `GET /api/v1/products/{id}` | `ProductDto` | `200` | `ETag` header |
| | `NotFound<ProductId>` | `404` | `code` member `NOT_FOUND` |
| | `Error` | `400` / `500` | `400` for `Error.ValidationFailureCode` (an empty GUID), `500` for any other code |
| `GET /api/v1/products` | `PagedResult<ProductDto>` | `200` | `X-Total-Count` and RFC 8288 `Link` headers; a page past the end is still `200` with empty `items` |
| | `ValidationErrors` | `400` | Per-field `errors` (`PageNumber`, `PageSize`, `MinPrice`, `MaxPrice`, `NameContains`, `OwnerId`, `Sort`) |
| | `Error` | `500` | |
| `PUT /api/v1/products/{id}` | `ProductDto` | `204` | No body; the new `ETag` header |
| | `NotFound<ProductId>` | `404` | |
| | `ValidationErrors` | `400` | Also a malformed `If-Match` (an error naming the header) |
| | `NotAuthorized` | `403` | Caller is not the owner |
| | `Conflict` | `409` | The new name duplicates another product's, up front or at commit |
| | `PreconditionFailed` | `412` | Stale `If-Match`, up front or at commit |
| | absent `If-Match` (`MissingIfMatch`) | `428` | Answered before anything is sent to MediatR |
| | `Error` | `500` | |
| `PATCH /api/v1/products/{id}` | `ProductDto` | `200` | The whole updated product and its new `ETag` |
| | `NotFound<ProductId>` | `404` | |
| | `ValidationErrors` | `400` | Nothing supplied, a supplied `null`, a bad value, or a malformed `If-Match` |
| | `NotAuthorized` | `403` | |
| | `Conflict` | `409` | |
| | `PreconditionFailed` | `412` | |
| | body not `application/merge-patch+json` | `415` | Rejected by `[Consumes]` |
| | absent `If-Match` (`MissingIfMatch`) | `428` | |
| | `Error` | `500` | |
| `DELETE /api/v1/products/{id}` | `Success` | `204` | No body, no `ETag` |
| | `NotFound<ProductId>` | `404` | |
| | `NotAuthorized` | `403` | Caller is not an administrator; checked before validation |
| | `PreconditionFailed` | `412` | Only when `If-Match` was sent and is stale, up front or at commit |
| | malformed `If-Match` | `400` | An absent `If-Match` is fine and deletes whatever version is stored |
| | `Error` | `400` / `500` | `400` for `Error.ValidationFailureCode` (an empty GUID; this union has no `ValidationErrors` case), `500` otherwise |
| `POST /api/v1/impersonation/tokens` | `ImpersonationToken` | `200` | The token, its expiry and effective identity; `Cache-Control: no-store` on every response of this endpoint. See [Impersonation](impersonation.md#impersonation-acting-as-another-identity) |
| | `ValidationErrors` | `400` | Per-field `errors` (`Reason`, `TargetUserId`, `LifetimeMinutes`, `Roles`, `TicketReference`) |
| | `NotAuthorized` | `403` | Neither `Administrator` nor `Support` (checked first); already impersonating; a role outside `AssignableRoles`; a role a non-administrator does not hold |
| | `Error` (`IMPERSONATION_DISABLED`) | `404` | The feature is switched off; answered to every authenticated caller before the pipeline runs |
| | `Error` | `500` | |

Outside the union: the framework itself answers a body that cannot be bound (`400`), an unmatched
route (`404`, including a non-GUID `{id}` and an API version this host does not serve; `401` first
when the caller is anonymous, since the fallback policy covers unmatched requests too) and an
unsupported media type (`415`) with the same problem shape and trace id, `GlobalExceptionHandler`
answers an unexpected exception with `500`, the rate limiter answers `429` and the timeout middleware answers `504`.
The `Error` to status table is configurable (`HttpMappingOptions.ErrorStatusCodes`). The OpenAPI
document at `/openapi/v1.json` declares these responses, the `ETag` and paging response headers,
and example bodies.

## API versioning

The version is a URL segment: every product and impersonation route is
`/api/v{version}/...` and this host serves version `1.0` (`[ApiVersion("1.0")]` on the controllers,
`Asp.Versioning.Mvc`). Health checks, the OpenAPI documents and Scalar are not versioned. Versioning
lives in the Api project only; commands, handlers and persistence know nothing of it.

- **Reported versions.** Every response to a versioned route, whatever its status, carries
  `api-supported-versions: 1.0` (`ReportApiVersions`).
- **Generated URLs are versioned.** The `Location` of a `201` and every URL in the `Link` paging
  header always use the canonical `/api/v1/...` form, whichever route the client used, so a client
  never follows a link back onto the unversioned alias. (`/api/v1.0/...` is accepted too and is
  served identically; the URLs it gets back use the `v1` spelling.)
- **A version that is not served** (`/api/v9/products`, `/api/vabc/products`) is not a route: it is
  answered like any other unmatched URL, a `404` `application/problem+json` with the `traceId` and
  `X-Trace-Id` (`401` first when the caller is anonymous). With the version in the path there is no
  separate "unsupported version" status.
- **One OpenAPI document per version**, `/openapi/v1.json` for version 1, listing only the
  versioned paths; Scalar's picker lists each document. Every transformer applies to every document.
  `Asp.Versioning.OpenApi` is not used: it needs `Microsoft.OpenApi` 2.x, while
  `Microsoft.AspNetCore.OpenApi` 11 needs 3.x, so the documents are registered by hand
  (`AddVersionedOpenApi`) and grouped by the API explorer (`Asp.Versioning.Mvc.ApiExplorer`).
- **Transitional unversioned alias.** `/api/products` and `/api/impersonation/tokens` still work,
  with identical behaviour, as an alias for version 1 (`AssumeDefaultVersionWhenUnspecified` with
  default `1.0`, and a second `[Route]` on each controller). The alias is not in the OpenAPI
  document. It exists so existing callers can move; to retire it, delete the
  `ApiVersions.UnversionedAliasPrefix` `[Route]` on `ProductsController` and
  `ImpersonationController`, set `AssumeDefaultVersionWhenUnspecified` to `false`, and delete the
  alias tests in `ApiVersioningTests` (the `Unversioned` members of `ApiRoutes`).
- **Adding version 2.** Give the new controller (or actions) `[ApiVersion("2.0")]`, add the `V2`
  values to `ApiVersions` (its `Location`/`Link` URLs are built from them, as version 1's are), and
  register `AddVersionedOpenApi("v2")`; `api-supported-versions` then lists both, and
  `/openapi/v2.json` and Scalar's picker include the new document.

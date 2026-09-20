# Partial updates: `PATCH` as JSON Merge Patch

Part of the [documentation](index.md).

`PUT` replaces a product's name and price in full; `PATCH /api/v1/products/{id}` changes only the
fields the request names. The body is a **JSON Merge Patch** ([RFC 7396](https://www.rfc-editor.org/rfc/rfc7396)):
each member is either *absent* (leave that field alone) or *present* (replace it).

```bash
# Rename only; the price is untouched. 200 with the whole product and the new ETag: W/"2"
curl -i -X PATCH localhost:5233/api/v1/products/$ID -H "Authorization: Bearer $ALICE" -H 'If-Match: W/"1"' \
  -H "Content-Type: application/merge-patch+json" -d '{"name":"Widget Pro"}'
```

- **Semantics.** Absent leaves a field alone; present replaces it. A field that is present as JSON
  `null` is a per-field `400`, *not* "clear it": name and price are required, so there is nothing to
  clear. A body that names neither field (`{}`, or only members this API does not know) is a `400`
  too, so a patch that would change nothing never advances the version. Members the contract does
  not know are **ignored** (RFC 7396 leaves unrecognised members to the recipient), so a client
  cannot smuggle `ownerId` or `id` in; they are silently dropped. Validation errors are keyed by
  property (`Name`, `Price`) exactly as for `PUT`, and the name and price rules are the *same* rules
  create and update use ([`ProductRuleExtensions`](../src/MediatrUnionPoc.Application/Features/Products/Common/ProductRuleExtensions.cs)).
- **Media type.** The body must be sent as `Content-Type: application/merge-patch+json`; anything
  else, including plain `application/json`, is a `415` problem. That is `[Consumes]` on the action:
  the framework's default JSON input formatter already accepts any `application/*+json`, so no
  custom formatter is needed for the body to bind. The OpenAPI document declares the request body
  as `application/merge-patch+json` (`ConsumesMediaTypeTransformer`), describes `name` and `price`
  as optional plain string/number members (`OptionalSchemaTransformer`) and carries a one-field
  example.
- **`Optional<T>`.** "Absent" versus "present as null" cannot be told apart with a nullable, so the
  command carries [`Optional<T>`](../src/MediatrUnionPoc.Application/Common/Optional.cs): a struct that
  is absent at `default` and present via `Optional<T>.Of(value)` (a present value may itself be
  `null` when `T` is nullable — `PatchProductCommand` uses `Optional<string?>` and
  `Optional<decimal?>`, which is how an explicit `null` reaches the validator). It has no
  serializer dependency; binding lives in the Api project as
  [`OptionalJsonConverterFactory`](../src/MediatrUnionPoc.Api/Http/OptionalJsonConverterFactory.cs), a
  `JsonConverterFactory` for any `Optional<T>` registered once on the MVC JSON options: a missing
  member leaves the property at `default` (absent), anything else, `null` included, is present.
- **Same guarantees as `PUT`.** `If-Match` is required (`428` absent, `400` malformed, `412`
  stale), only the product's owner may patch it (`403`), an unknown id is `404`, and renaming onto
  another product's name is `409`. The handler shares the load, ownership and version steps with
  `UpdateProductHandler` through `LoadForChangeAsync`
  ([`ProductChangeExtensions`](../src/MediatrUnionPoc.Application/Features/Products/Common/ProductChangeExtensions.cs)),
  and asks `ExistsWithNameAsync` only when the patch moves the product to a *different normalised*
  name, so patching a name to itself (even re-cased) is never a conflict. A commit-time
  `ConcurrencyConflict` / `UniqueViolation` becomes `412` / `409` through
  `PatchProductResult.FromCommitFailure`, so two racing patches with the same `ETag` yield exactly
  one `200` and one `412`.
- **One version bump.** The handler makes a single `Product.ApplyChanges(name, price)` call, which
  applies whichever fields are non-null and advances `Version` exactly once; `CreatedAt`, `OwnerId`
  and `Id` are never touched, and patching only the price leaves `Name` and `NormalizedName` alone.
- **Success is `200`, not `204`.** Unlike `PUT`, a successful patch returns the updated
  `ProductDto` (so the client sees the fields it did not send) together with the new `ETag`.

| `PatchProductResult` case | Status |
| --- | --- |
| `ProductDto` | `200` with the product and the new `ETag` |
| `ValidationErrors` (nothing supplied, a supplied `null`, a bad value, a malformed `If-Match`) | `400` |
| `NotAuthorized` | `403` |
| `NotFound<ProductId>` | `404` |
| `Conflict` (duplicate name, up front or at commit) | `409` |
| `PreconditionFailed` (stale `If-Match`, up front or at commit) | `412` |
| non-`application/merge-patch+json` body | `415` |
| absent `If-Match` | `428` |
| `Error` | `500` |

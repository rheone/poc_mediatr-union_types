# Optimistic concurrency: `ProductVersion`, `ETag` and `If-Match`

Part of the [documentation](index.md).

Two clients that load the same product, edit it, and save would otherwise silently overwrite each
other (a lost update). This POC prevents that with **optimistic concurrency**: no locks are held
while a client thinks; instead each write says which version it was based on, and a write based on
a version that is no longer current is refused.

- **The version is a domain value.**
  [`ProductVersion`](../src/MediatrUnionPoc.Domain/ProductVersion.cs) is a Vogen value object over a
  `long`. A new product starts at `ProductVersion.Initial` (1) and `Product.UpdateDetails` and `Product.ApplyChanges` advance
  it with `Version.Next()` on every mutation; the database does not generate it. Infrastructure
  maps it with a hand-written `ProductVersionValueConverter` (the same convention as `ProductId`
  and `Money`, keeping Domain free of EF Core) and marks it `IsConcurrencyToken()`, so every
  `UPDATE`/`DELETE` EF issues is conditioned on the version that was loaded. If no row matches, EF
  raises `DbUpdateConcurrencyException` and `EfCoreUnitOfWork.CommitAsync` reports it as
  `ConcurrencyConflict` (see [A commit can fail too](case-types.md#a-commit-can-fail-too-icommitfailable)).
- **On the wire it is a weak ETag.** `ProductVersion.ToETag()` renders `W/"3"` and
  `ProductVersion.ParseETag` reads it back (anything else, including a strong tag or `*`, is not
  a version). `GET /api/v1/products/{id}` and `POST /api/v1/products` return it in the `ETag` header and
  `ProductDto` carries the same number in its `version` member; a successful `PUT` answers `204` (and
  a successful `PATCH` `200` with the product) with the *new* `ETag`.
- **`If-Match` carries it back.** `PUT` and `PATCH` require it: absent is `428 Precondition Required`,
  present but not a well-formed tag is a `400` validation problem naming the header. `DELETE` treats
  it as optional — absent deletes whatever is stored, present is enforced. Parsing lives in one place,
  [`IfMatchHeader.Parse`](../src/MediatrUnionPoc.Api/Http/IfMatchHeader.cs), a small union of
  `ProductVersion`, `MissingIfMatch` and `ValidationErrors`.
- **A stale version is a `412`.** `UpdateProductCommand.ExpectedVersion` and
  `PatchProductCommand.ExpectedVersion` (required) and
  `DeleteProductCommand.ExpectedVersion` (optional) travel with the command; the handler compares
  it to the loaded product first and returns `PreconditionFailed`, so most stale writes never reach
  the database. A request that passes that check and then loses a race to another writer is
  caught by the concurrency token at commit, and each union's `FromCommitFailure` turns it into the
  same `PreconditionFailed`. Two racing `PUT`s with the same `ETag` therefore yield exactly one `204`
  and one `412`.

`PreconditionFailed` (412) and `Conflict` (409, for a collision with existing state — here a
duplicate product name) are shared case types in the same sense as `NotFound`: meaning-free records whose
HTTP mapping lives in
[`ResultHttpExtensions`](../src/MediatrUnionPoc.Api/Http/ResultHttpExtensions.cs) as extension members,
with RFC 7807 bodies and the trace id like every other problem response. The OpenAPI document
declares the `409`/`412`/`428` responses (with example bodies) and the `ETag` response header.

### Duplicate product names

A product name is a duplicate if it matches another product's name **ignoring case and
surrounding whitespace** (`"  BLUE widget "` collides with `"Blue Widget"`), across all owners.
`Create`, `Update` and `Patch` can therefore return `Conflict` (`Delete` cannot). The rule is enforced in
two layers that share one definition:

- **The Domain owns the rule.** [`ProductNames.Normalize`](../src/MediatrUnionPoc.Domain/ProductNames.cs)
  (trim, then upper-case with the invariant culture) is the one canonical comparison key, and
  `Product.NormalizedName` keeps it in step with `Name` on every create and rename.
- **Up front, in the handler.** `IProductRepository.ExistsWithNameAsync(name, excludingId, ct)` asks
  whether another product already holds an equivalent name; on `true` the handler returns
  `Conflict` before mutating anything. `Update` passes the product's own id as `excludingId`, so
  resubmitting a product's own name (even re-cased) is never a conflict with itself. The problem
  body's `detail` echoes only the name the caller supplied, never anything about the product that
  holds it.
- **At commit, as the race backstop.** The check reads committed state, so two simultaneous requests
  can both pass it. A unique index on `NormalizedName` (Infrastructure's `AppDbContext`) settles the
  race: the loser's `SaveChanges` fails, `EfCoreUnitOfWork.CommitAsync` reports `UniqueViolation`,
  and `FromCommitFailure` turns that into the same `Conflict` — so two racing `POST`s with the same
  name yield exactly one `201` and one `409`. Storing the normalised key as a plain column keeps the
  index database-agnostic (no collation), and the translation only treats a unique violation on that
  index as `UniqueViolation`; any other constraint failure (a primary-key collision, say) still
  propagates as the unexpected fault it is. Identifying the index relies on SQLite's message naming
  the column (`Products.NormalizedName`); another provider needs its own check there.

```bash
# Create: 201 with ETag: W/"1"
curl -i -X POST localhost:5233/api/v1/products -H "Authorization: Bearer $ALICE" -H "Content-Type: application/json" \
  -d '{"name":"Widget","price":9.99}'

# No If-Match: 428.  Stale If-Match: 412.  Current If-Match: 204 with ETag: W/"2"
curl -i -X PUT localhost:5233/api/v1/products/$ID -H "Authorization: Bearer $ALICE" -H 'If-Match: W/"1"' \
  -H "Content-Type: application/json" -d '{"name":"Widget Pro","price":19.99}'
```

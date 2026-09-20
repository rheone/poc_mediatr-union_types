# Worked example: `UpdateProductCommand`, case by case

Part of the [documentation](index.md).

The diagrams above show the pipeline shape in the abstract. This one traces a single, concrete
request — `PUT /api/v1/products/{id}` — all the way through, branching at every point where a
different case of [`UpdateProductResult`](../src/MediatrUnionPoc.Application/Features/Products/Update/UpdateProductResult.cs)
(`union(ProductDto, NotFound<ProductId>, ValidationErrors, Error, NotAuthorized, PreconditionFailed, Conflict)`)
could come back. Each terminal branch is tagged with the case type it produces («ProductDto», «NotFound»,
«ValidationErrors», «Error», «NotAuthorized», «PreconditionFailed», «Conflict») and color-coded so
the same case is easy to follow from where it's created to the HTTP status it becomes. (`PATCH`
follows the same path with the same seven cases, and answers `200` with the product instead of `204`.)

Six of the seven cases are actually reachable from
[`UpdateProductHandler`](../src/MediatrUnionPoc.Application/Features/Products/Update/UpdateProductHandler.cs)
as written today, including the resource-based `ProductOwner` check described in
[Authorization](authorization.md#authorization) above. `«Error»` is included because the union *declares* it as a
possible outcome — reserved for a future unexpected-failure path — even though nothing in the
current handler produces it; the diagram marks that branch as dashed for exactly this reason. A
missing or malformed `If-Match` never reaches this diagram: the controller answers `428` or `400`
before sending the command.

```mermaid
flowchart TD
    classDef success fill:#d8f5d0,stroke:#2f9e44,stroke-width:2px;
    classDef notfound fill:#fff3bf,stroke:#e8590c,stroke-width:2px;
    classDef validation fill:#ffe3e3,stroke:#c92a2a,stroke-width:2px;
    classDef error fill:#f1f3f5,stroke:#495057,stroke-width:2px,stroke-dasharray: 4 3;
    classDef notauthorized fill:#e5dbff,stroke:#7048e8,stroke-width:2px;
    classDef precondition fill:#d0ebff,stroke:#1971c2,stroke-width:2px;
    classDef conflict fill:#ffec99,stroke:#f08c00,stroke-width:2px;

    Client(["PUT /api/v1/products/{id}<br/>If-Match: W/&quot;n&quot;<br/>body: name, price"]) --> Ctrl["ProductsController.Update"]
    Ctrl --> Build["new UpdateProductCommand(id, name, price, principal, expectedVersion)"]
    Build --> Send["sender.Send(command)"]

    Send --> Log1["LoggingBehavior<br/>log: handling UpdateProductCommand"]
    Log1 --> Val{"ValidationBehavior<br/>FluentValidation passes?"}

    Val -->|"no"| VErr["«ValidationErrors»<br/>UpdateProductResult.FromValidationErrors(errors)"]:::validation
    VErr --> SkipTx["handler and TransactionBehavior<br/>never run"]:::validation
    SkipTx --> Log2a["LoggingBehavior<br/>log: result = ValidationErrors"]:::validation
    Log2a --> Map1["controller switch"]:::validation
    Map1 --> R400["400 Bad Request"]:::validation

    Val -->|"yes"| Begin["TransactionBehavior<br/>BeginTransactionAsync()"]
    Begin --> Handle["UpdateProductHandler.Handle"]
    Handle --> Lookup{"repository.GetByIdAsync(id)"}

    Lookup -->|"null"| NF["«NotFound»<br/>new NotFound(id)"]:::notfound
    NF --> Roll1["TransactionBehavior<br/>RollbackAsync()"]:::notfound
    Roll1 --> Log2b["LoggingBehavior<br/>log: result = NotFound"]:::notfound
    Log2b --> Map2["controller switch"]:::notfound
    Map2 --> R404["404 Not Found"]:::notfound

    Lookup -->|"found"| Own{"ResourceAuthorizationService.AuthorizeAsync:<br/>ProductOwner policy?"}

    Own -->|"no"| NAuth["«NotAuthorized»<br/>UpdateProductResult.FromNotAuthorized(notAuthorized)"]:::notauthorized
    NAuth --> Roll3["TransactionBehavior<br/>RollbackAsync()"]:::notauthorized
    Roll3 --> Log2e["LoggingBehavior<br/>log: result = NotAuthorized"]:::notauthorized
    Log2e --> Map5["controller switch"]:::notauthorized
    Map5 --> R403["403 Forbidden"]:::notauthorized

    Own -->|"yes"| Ver{"product.Version == expectedVersion?"}

    Ver -->|"no"| Stale["«PreconditionFailed»<br/>new PreconditionFailed(message)"]:::precondition
    Stale --> Roll4["TransactionBehavior<br/>RollbackAsync()"]:::precondition
    Roll4 --> Log2f["LoggingBehavior<br/>log: result = PreconditionFailed"]:::precondition
    Log2f --> Map6["controller switch"]:::precondition
    Map6 --> R412["412 Precondition Failed"]:::precondition

    Ver -->|"yes"| Name{"repository.ExistsWithNameAsync(name, excludingId: id)"}

    Name -->|"yes"| Dup["«Conflict»<br/>ProductConflicts.NameTaken(name)"]:::conflict
    Dup --> Roll5["TransactionBehavior<br/>RollbackAsync()"]:::conflict
    Roll5 --> Log2g["LoggingBehavior<br/>log: result = Conflict"]:::conflict
    Log2g --> Map7["controller switch"]:::conflict
    Map7 --> R409["409 Conflict"]:::conflict

    Name -->|"no"| Update["product.UpdateDetails(name, price)<br/>Version = Version.Next()"]
    Update --> Ok["«ProductDto»<br/>ProductDto.FromDomain(product)"]:::success
    Ok --> Commit["TransactionBehavior<br/>CommitAsync() then SaveChangesAsync()"]:::success
    Commit -->|"a concurrent write won the race:<br/>CommitAsync returns ConcurrencyConflict"| Stale2["Rollback, then<br/>FromCommitFailure(ConcurrencyConflict)<br/>= «PreconditionFailed»"]:::precondition
    Commit -->|"a concurrent request took the name:<br/>CommitAsync returns UniqueViolation"| Dup2["Rollback, then<br/>FromCommitFailure(UniqueViolation)<br/>= «Conflict»"]:::conflict
    Stale2 --> Map6
    Dup2 --> Map7
    Commit --> Log2c["LoggingBehavior<br/>log: result = ProductDto"]:::success
    Log2c --> Map3["controller switch"]:::success
    Map3 --> R204["204 No Content + new ETag"]:::success

    Handle -.->|"declared, not exercised today"| Err["«Error»<br/>new Error(message, code)"]:::error
    Err -.-> Roll2["TransactionBehavior<br/>RollbackAsync()"]:::error
    Roll2 -.-> Log2d["LoggingBehavior<br/>log: result = Error"]:::error
    Log2d -.-> Map4["controller switch"]:::error
    Map4 -.-> R500["500 Internal Server Error"]:::error
```

Reading the diagram:

- **Green («ProductDto»)** is the only path where `TransactionBehavior` commits — everything else
  rolls back or never opens a transaction at all.
- **Blue («PreconditionFailed»)** has two sources that end in the same case: the handler's
  up-front version check (most stale writes never reach the database), and the commit-time
  `ConcurrencyConflict` when two requests both passed that check and one lost the race — the union's
  `FromCommitFailure` maps both to the one outcome the caller can act on. See
  [Optimistic concurrency](concurrency.md#optimistic-concurrency-productversion-etag-and-if-match).
- **Orange («Conflict»)** likewise has two sources ending in one case: the handler's up-front name
  check, and the commit-time `UniqueViolation` when two requests both passed that check. See
  [Duplicate product names](concurrency.md#duplicate-product-names).
- **Red («ValidationErrors»)** short-circuits *before* `TransactionBehavior` even runs — no
  transaction is opened for input that never should have reached the handler.
- **Yellow («NotFound»)**, **purple («NotAuthorized»)**, and **dashed grey («Error»)** all reach
  the handler, open a transaction, and get rolled back — the difference between them is only which
  case type the handler chose to return, not any `try`/`catch` structure.
- **Purple («NotAuthorized»)** is the one branch that depends on a second lookup beyond the
  entity's existence — the caller's identity has to match the product's owner, not just the
  product having to exist — which is why it's checked only after `Lookup` already succeeded, the
  same ordering [Authorization](authorization.md#authorization) describes for resource-based checks generally.
- Every branch still passes back through `LoggingBehavior` on the way out, so every outcome —
  success or not — gets logged exactly once, symmetrically.

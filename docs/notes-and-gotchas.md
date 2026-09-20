# Notes and gotchas

Part of the [documentation](index.md).

- **Vogen + `PrivateAssets="all"`:** don't set this on the `Vogen` package reference. Vogen ships
  a small runtime support assembly (`Vogen.SharedTypes.dll`) alongside its source generator;
  `PrivateAssets="all"` strips that runtime assembly from consuming projects too, causing a
  `FileNotFoundException` at runtime, not just a build warning.
- **Vogen + EF Core:** Vogen can generate an `EfCoreValueConverter` nested type directly on the
  value object, but that requires the value object's own project to reference EF Core — which
  leaks a persistence concern into the Domain layer. This repo keeps Domain persistence-agnostic
  (`Conversions.SystemTextJson` only) and hand-writes `ValueConverter<T,TPrimitive>` classes in
  Infrastructure instead — see [`ValueConverters.cs`](../src/MediatrUnionPoc.Infrastructure/ValueConverters.cs).
- **Persistence is SQLite, so transactions are real.** `EfCoreUnitOfWork.BeginTransactionAsync`
  always opens a database transaction (a failure to open one propagates), commit saves then commits
  it, and rollback rolls it back and detaches every tracked entry so a later commit cannot persist
  staged changes. Runtime and tests both use SQLite; with no `ConnectionStrings:Products` value the
  app uses a private in-memory database kept alive by one open connection, with the schema created
  at startup (`EnsureCreated`, no migrations). The class is named for the persistence technology it
  adapts (EF Core), not for a provider: a future NHibernate adapter would be a separate
  `IUnitOfWork` implementation. `CommitAsync` returns a `CommitResult` union rather than `Task`: a
  concurrency-token failure comes back as `ConcurrencyConflict` (nothing persisted; the caller
  rolls back), and a unique-constraint failure on the product-name index comes back as `UniqueViolation`.
- **NSubstitute + Vogen structs:** two `Arg.Any<T>()` matchers in the same mocked call, where one
  `T` is a Vogen value object (custom equality), can throw `AmbiguousArgumentsException`. Use a
  concrete value object instance instead of `Arg.Any<T>()` for at least one of the arguments.
- **Central Package Management** (`Directory.Packages.props`) pins every package version once at
  the solution root; individual `.csproj` files reference packages without a `Version` attribute.
- **Async naming convention:** every async method this repo owns the signature of ends in `Async`
  and takes a `CancellationToken cancellationToken = default` — required in the sense that callers
  who have a token should pass it, defaulted so call sites that don't (tests, REPL-style usage)
  aren't forced to pass `CancellationToken.None` everywhere. The one exception is `Handle` on every
  `IRequestHandler<TRequest,TResponse>` and `IPipelineBehavior<TRequest,TResponse>` implementation
  (handlers, `LoggingBehavior`, `ValidationBehavior`, `TransactionBehavior`) — MediatR's interfaces
  fix that method's name and require the token be non-defaulted, so those implementations
  deliberately don't follow the convention; they can't without breaking the interface.
- **ASP.NET Core trims "Async" from controller action names by default.** `MvcOptions.SuppressAsyncSuffixInActionNames`
  defaults to `true`, which would register `ProductsController.GetByIdAsync` as action name
  `"GetById"` — breaking `Url.Action(nameof(GetByIdAsync), ...)`'s link generation (how `Location` is built), since
  `nameof` gives the C# identifier, not the trimmed action name MVC would otherwise register it
  under. `Program.cs`'s `AddControllers(...)` call sets `SuppressAsyncSuffixInActionNames = false`
  so action names keep the `Async` suffix this repo's naming convention requires everywhere else.
  [`ProductsControllerTests`](../tests/MediatrUnionPoc.Api.IntegrationTests/ProductsControllerTests.cs) exercises
  the real ASP.NET Core host end to end, including following the `Location` header a create
  returns, specifically so a link-generation mismatch like this can't pass silently.
- **Compile-time proof, not just documentation:** [`ExhaustivenessTests`](../tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs)
  shells out to `dotnet build` against four tiny scratch projects under `tests/CompileTimeChecks/`
  (excluded from `MediatrUnionPoc.slnx` on purpose) to prove a non-exhaustive `switch` over a union
  — and, separately, a non-exhaustive `ITransactionOutcome.ShouldCommit` — is a real compiler error
  (`CS8509`), using the exact installed SDK compiler rather than an in-process Roslyn NuGet
  package — the latter could easily predate this brand-new preview language feature and silently
  fail to reproduce the behavior being tested.

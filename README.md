# MediatrUnionPoc

A proof of concept: can C#'s new [`union`](#the-c-union-type) type serve as the response type for a
[MediatR](#mediatr-vocabulary)[^mediatr-license]/[CQRS](#architectural-patterns) handler, replacing
the usual "throw an exception or return null" grab-bag with a closed, exhaustively-checked set of
outcomes?

Short answer: yes, and it composes well with MediatR pipeline behaviors via generic constraints on
**[static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)**.
Details below.

> [!IMPORTANT]
> The `union` keyword is a **C# 15** feature — it needs **.NET 11 Preview 5+**.
> This repo targets `net11.0` with `<LangVersion>preview</LangVersion>` and was built
> against the .NET 11 RC1 SDK.

> [!NOTE]
> **This is one opinionated take, not a prescription.** This repo starts from the position that
> exceptions should be reserved for truly exceptional circumstances — not for ordinary business
> outcomes like "not found" or "validation failed" — and builds a set of suggested conventions
> around that position: unions as response types, meaning-free shared case types, and pipeline-based
> cross-cutting concerns. There are many other defensible ways to solve the same problem — a
> hand-rolled `Result<T>`, a third-party library, or exceptions used deliberately and consistently
> could all work for a different team. Treat what follows as "one thing that works," not "the
> correct answer."

## Table of contents

- [Getting started](#getting-started)
  - [Project layout](#project-layout)
- [Motivation](#motivation)
- [What this pattern provides, and its actual scope](#what-this-pattern-provides-and-its-actual-scope)
- [Core concepts](#core-concepts)
  - [The C# `union` type](#the-c-union-type)
  - [Static abstract interface members: why generic code can build a union it's never seen](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)
  - [Case types used here](#case-types-used-here)
  - [Shared case types are meaning-free](#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means)
  - [No exceptions for expected outcomes](#no-exceptions-for-expected-outcomes)
  - [Transactions: what rollback undoes, and why it matters](#transactions-what-rollback-undoes-and-why-it-matters)
  - [Unit of Work: one session, every repository](#unit-of-work-one-session-every-repository)
  - [Vogen: avoiding primitive obsession](#vogen-avoiding-primitive-obsession)
- [Adding a new command or query](#adding-a-new-command-or-query)
- [Request lifecycle](#request-lifecycle)
- [Authorization](#authorization)
  - [Why two different points in the request lifetime](#why-two-different-points-in-the-request-lifetime)
  - [How the two flows fit together](#how-the-two-flows-fit-together)
  - [Role-based: `IRequiresAuthorization` + `AuthorizationBehavior`](#role-based-irequiresauthorization-authorizationbehavior)
  - [Resource-based: `ResourceAuthorizationService` + `OwnerAuthorizationHandler<TResource>`](#resource-based-resourceauthorizationservice-ownerauthorizationhandlertresource)
  - [Zero-to-many handlers, and multiple requirements](#zero-to-many-handlers-and-multiple-requirements)
  - [Where the identity comes from](#where-the-identity-comes-from)
  - [Configuring role-based authorization for a new command](#configuring-role-based-authorization-for-a-new-command)
  - [Configuring resource-based authorization for a new command](#configuring-resource-based-authorization-for-a-new-command)
  - [Why not `IAuthorizationRequirementData` attributes](#why-not-iauthorizationrequirementdata-attributes)
- [Worked example: `UpdateProductCommand`, case by case](#worked-example-updateproductcommand-case-by-case)
- [Extending the pattern: syncing a search index](#extending-the-pattern-syncing-a-search-index)
- [Speculative shared case types for a larger API](#speculative-shared-case-types-for-a-larger-api)
- [Notes and gotchas](#notes-and-gotchas)
- [Glossary](#glossary)
- [Footnotes](#footnotes)

## Getting started

```bash
dotnet build
dotnet test
dotnet run --project src/MediatrUnionPoc.Api
```

`global.json` pins the SDK to the exact `11.0.100-rc.1...` preview build this repo was written
against. Without it, an IDE's own SDK resolver (Visual Studio in particular) can silently fall
back to the newest *stable* SDK it finds and fail with `NETSDK1045` ("does not support targeting
.NET 11.0") — `allowPrerelease: true` is what tells it a preview SDK is expected here, not a
missing `<TargetFramework>` value. If VS still shows the error after pulling this file, close and
reopen the solution so it re-resolves.

### Project layout

| Project                          | Responsibility                                                            |
| --------------------------------- | --------------------------------------------------------------------------- |
| `MediatrUnionPoc.Domain`          | Entities, [Vogen](#vogen-avoiding-primitive-obsession) [value objects](#vogen-vocabulary) (`ProductId`, `Money`), repository/UoW interfaces |
| `MediatrUnionPoc.Application`     | Commands, queries, handlers, union result types, validators, pipeline behaviors |
| `MediatrUnionPoc.Infrastructure`  | EF Core `DbContext`, repository + unit-of-work implementations             |
| `MediatrUnionPoc.Api`             | Controllers that map each union to an `IActionResult`                      |
| `MediatrUnionPoc.Domain.Tests`    | Unit tests for `Money`, `ProductId`, and `Product`                          |
| `MediatrUnionPoc.Application.Tests` | xUnit v3 + NSubstitute — union mechanics, pipeline behaviors, handlers, validators |
| `MediatrUnionPoc.Infrastructure.IntegrationTests` | Real EF Core SQLite provider (in-memory database), end to end |
| `MediatrUnionPoc.Api.IntegrationTests` | `WebApplicationFactory`-based Api integration tests           |
| `MediatrUnionPoc.ArchitectureTests` | `NetArchTest.Rules` assertions enforcing the layering above               |

Application code is organized as **[vertical slices](#architectural-patterns)** under
`Features/Products/<Operation>/` (`Create`, `Update`, `Delete`, `GetById`, `GetPaged`) — everything
for one operation (command/query, validator, handler, result union) lives together, rather than
being split across horizontal "Commands/Handlers/Validators" folders.

## Motivation

Most CQRS handlers end up with a response shape that's either:

- **A single DTO**, with failures signaled by throwing (`NotFoundException`, `ValidationException`,
  ...) and caught somewhere upstream, or
- **A hand-rolled "Result" wrapper** (`Result<T>`, `OneOf<T1,T2,...>`, `ErrorOr<T>`) from a
  third-party library, because C# had no first-class closed-union type to express
  "exactly one of these N things came back."

`union` gives you option two, built into the language, with compiler-enforced
[exhaustiveness](#c-language-concepts) at every [`switch`](#c-language-concepts). This project
pushes that as far as it reasonably goes: every command and query returns a `union` of whatever
outcomes are actually possible for that operation — no more, no fewer — and nothing in the request
pipeline throws for an outcome it expected to see.

## What this pattern provides, and its actual scope

> [!NOTE]
> **This is a convention, not a MediatR requirement.** `IRequestHandler<TRequest, TResponse>`
> places no constraint on `TResponse` beyond being a type — a handler is free to return a plain
> DTO, a `bool`, a `Task` with nothing meaningful in it, or anything else, and MediatR is
> completely indifferent to unions. Returning a `union` of shared and bespoke case types is
> *this repo's own deliberate convention* for expressing "exactly one of N meaningful outcomes,"
> adopted because it fits CQRS-style commands/queries that can genuinely end several different
> ways — not something MediatR asks for, and not the only valid return shape for a handler even
> within this codebase's own pattern. A trivial operation with only one possible outcome has no
> reason to introduce a union at all.

### Union types as responses

- **Exhaustiveness is enforced by the compiler.** Add a new case to a union, and every `switch`
  over it stops compiling until you handle the new case. A forgotten `if (result == null)` check
  simply can't happen — there's no null, only the cases you declared.
- **The signature *is* the contract.** `Task<CreateProductResult>` where
  `CreateProductResult` is `union(ProductDto, ValidationErrors, Error)` tells a caller everything
  that can happen without reading the method body or any docs.
- **Mix-and-match per operation.** A union is declared per use case, not shared globally — a
  "get" query might only ever produce `(Dto, NotFound, Error)` while a "delete" produces
  `(Success, NotFound, Error)`. No forcing every endpoint through one bloated `Result` type with
  irrelevant properties.
- **No boxing tax for the common path.** Case types are checked directly against the union's
  underlying `object? Value` at pattern-match sites; the union itself is a lightweight struct.
- **Generic code can still build a union it doesn't know the shape of.** By having each union
  implement an interface with a **static abstract member** (`static abstract TSelf
  FromValidationErrors(ValidationErrors errors)`), a fully generic `ValidationBehavior<TRequest,
  TResponse>` can construct the right concrete union's error case without ever naming it — see
  [`IValidatable<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/IValidatable.cs) and
  [Static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)
  below.

## Core concepts

The sections below cover the mechanics and vocabulary the pattern is built on, roughly in the order
they build on each other. Read what's relevant to what you're doing; the [Glossary](#glossary) at
the end is reference material, not required front-to-back reading.

### The C# `union` type

> [!TIP]
> Deeper, citation-backed research behind this section lives in
> [`docs/research/csharp-union-type-research.md`](docs/research/csharp-union-type-research.md) —
> primary sources (language reference, feature spec, [LDM](#cross-cutting-concepts) issue, compiler
> bug tracker) for every claim below, plus open questions not yet settled upstream.

```csharp
public union CreateProductResult(ProductDto, ValidationErrors, Error);
```

This declares a closed set of three **[case types](#this-repos-own-types)**. The compiler generates
a [struct](#c-language-concepts) implementing `IUnion { object? Value { get; } }`, plus an implicit
conversion from each case type:

```csharp
CreateProductResult ok = new ProductDto(id, "Widget", 9.99m);   // implicit conversion
CreateProductResult bad = new Error("boom", "BOOM");            // implicit conversion
```

[Pattern matching](#c-language-concepts) unwraps to the *contained* case, not the union wrapper
itself:

```csharp
var response = result switch
{
    ProductDto dto => Ok(dto),          // matches result.Value is ProductDto
    ValidationErrors e => BadRequest(e),
    Error err => Problem(err.Message),
    // no default/discard needed — the compiler knows these are the only three cases
};
```

Unions can carry a body, including implementing [interfaces](#c-language-concepts) — which is how
this repo gets a union to expose a static factory method usable from fully generic code (see
[Static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)
below). This only works because C# now allows **static abstract members on interfaces** —
without that, a generic pipeline behavior would have no way to construct an arbitrary union type
it has never seen.

#### Switch-and-unwrap: why controllers never return the union directly

Every action in [`ProductsController`](src/MediatrUnionPoc.Api/Controllers/ProductsController.cs)
`switch`es on the union and returns a plain DTO/`ProblemPayload`/status code — it never does
`return Ok(result)` with the raw union itself.

> [!NOTE]
> It's not because `System.Text.Json` would otherwise serialize the generated struct's own
> `Value` property as a wrapper. .NET 11's `System.Text.Json` has built-in awareness of `IUnion`
> and transparently flattens to whichever case type is boxed inside, with no wrapper at all:
> `JsonSerializer.Serialize((CreateProductResult)new ProductDto(...))` produces the exact same
> bytes as serializing the `ProductDto` directly. See
> [`UnionJsonSerializationTests`](tests/MediatrUnionPoc.Application.Tests/Unions/UnionJsonSerializationTests.cs)
> for the proof. `System.Text.Json` is simply this repo's own choice of serializer, not a
> requirement of the union pattern itself — it's what ASP.NET Core defaults to, and it happens to
> have this `IUnion` awareness in this preview build. A different serializer (`Newtonsoft.Json`,
> say) or a non-JSON boundary entirely (gRPC, a message queue's binary format) would need its own
> answer to "how do I represent one of N cases on the wire," or none at all if it never crosses a
> serialization boundary — nothing about the pattern in this repo *requires* System.Text.Json.

The real reason to switch first has nothing to do with serialization shape:

- **There is no wire-level case discriminator.** Even with automatic flattening, nothing in the
  JSON says *which* case came back. `Success` serializes to a completely uninformative `{}`; a
  `ProductDto` and a `NotFound` produce different-looking objects only because their fields happen
  to differ — there is no formal `"case"` or `"$type"` tag guaranteeing that. A consumer receiving
  raw bytes, outside the context of (say) an HTTP status code, cannot reliably tell "succeeded with
  nothing to report" apart from "an unrecognized case was added and I don't know what it means."
- **Each case still needs boundary-specific context attached.** A `NotFound` needs to become
  HTTP 404, not just "an object shaped like `{"Id": "..."}"`; a `ValidationErrors` needs to become
  an RFC 7807 ([Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807) — a
  standardized JSON shape for HTTP error responses) problem response with per-field errors. Nothing
  about serialization does that mapping — only code that inspects which case came back can, which
  is exactly what the `switch` in every controller action does.
- **The union is for code that's still in-process; the unwrapped result is for anything crossing a
  boundary.** Controller, queue consumer, CLI — whichever boundary the domain outcome meets,
  that's where the `switch` belongs, turning a domain outcome into whatever shape *that* boundary
  actually needs.

### Static abstract interface members: why generic code can build a union it's never seen

[`IValidatable<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/IValidatable.cs),
[`ITransactionOutcome<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/ITransactionOutcome.cs),
and [`IAuthorizable<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/IAuthorizable.cs)
all lean on the same language feature: a **static abstract interface member** — introduced in
**[C# 11 / .NET 7](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-11#static-abstract-members-in-interfaces)**
(November 2022), originally to support generic math (`INumber<T>` and similar interfaces). Before
it existed, an interface could only require *instance* members: `bool IsValid()` works fine when
you already have an instance to call it on, but a generic pipeline behavior like
`ValidationBehavior<TRequest, TResponse>` doesn't have a `TResponse` instance yet when validation
fails — it needs to *construct* one, generically, for a concrete union type it has never seen and
will never reference by name.

```csharp
public interface IValidatable<TSelf> where TSelf : IValidatable<TSelf>
{
    static abstract TSelf FromValidationErrors(ValidationErrors errors);
}
```

Because `FromValidationErrors` is `static abstract`, every *implementing type* — not every
instance — must supply it, and it becomes callable through a [generic](#c-language-concepts) type
parameter constrained to the interface: `TResponse.FromValidationErrors(errors)` compiles and
dispatches to whichever concrete union `TResponse` actually is at the call site, resolved via the
generic constraint (`where TResponse : IValidatable<TResponse>`), with no runtime type inspection
at all.

**Before C# 11, none of this could be expressed this cleanly.** The realistic workarounds were:

- **Reflection** — look up a static method by name/convention (`FromValidationErrors`) via
  `typeof(TResponse).GetMethod(...)` and invoke it dynamically. This throws away compile-time
  safety entirely: a typo in the method name, or a union that forgot to implement the convention,
  fails at runtime instead of at the build. It's also measurably slower than a direct call.
- **A required base class, or a factory delegate registered per type** — workable, but every new
  union then needs a line of manual registration somewhere central, and that central registry has
  to be kept in sync by hand as unions are added — exactly the kind of bookkeeping this pattern is
  trying to eliminate.
- **Assuming the shape instead of enforcing it** — trusting every union "just happens" to expose a
  compatible static method, with nothing checking that assumption until it's already wrong in
  production.

Static abstract interface members close that gap: the compiler enforces the contract at the
*implementing type's own declaration*, and generic code calls it with the same safety and
performance as a resolved instance-method call — no reflection, no registry, no runtime surprises.

### Case types used here

| Case               | Role    | Meaning                                                         |
| ------------------ | ------- | --------------------------------------------------------------- |
| `Success`          | Shared  | The command completed; no payload to return                     |
| `<Dto>`            | Bespoke | The operation's actual result payload (e.g. `ProductDto`)       |
| `NotFound`         | Shared  | The requested entity doesn't exist                              |
| `ValidationErrors` | Shared  | Input failed FluentValidation checks                            |
| `Error`            | Shared  | An unexpected/domain error, with a stable machine-readable code |
| `Failure`          | Shared  | Business-rule failure(s) that aren't input validation           |
| `NotAuthorized`    | Shared  | The caller isn't allowed to perform this operation              |

Every row but `<Dto>` is a **[shared case type](#this-repos-own-types)** — the same meaning-free
record reused across unions (see
[Shared case types are meaning-free](#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means)).
`<Dto>` stands for whatever **[bespoke case type](#this-repos-own-types)** carries that operation's
actual payload — `ProductDto` for the Products feature. "Bespoke" here means *not meaning-free* —
a `ProductDto` means exactly one thing, a successfully materialized product — not that it's
confined to a single union: it's the success case of `CreateProductResult` and
`GetProductByIdResult` alike, and appears again wrapped as `PagedResult<ProductDto>` inside
`GetPagedProductsResult`. That's a third, different kind of reuse from a shared case type's —
`ProductDto` is reused because every one of those operations happens to succeed with the same
payload shape, not because its identity is deliberately meaning-free the way `Success` or
`NotFound`'s is.

Each union in this repo declares only the subset of cases that operation can actually produce —
see `CreateProductResult` vs `UpdateProductResult` vs `GetProductByIdResult` for three different
mixes.

> [!WARNING]
> **Don't design case types as a mirror of HTTP status codes.** A union case describes what
> *happened in the domain* — it's meaning, not transport. `NotFound` doesn't mean "return 404";
> it means "the thing wasn't there," full stop. The mapping to a status code (or a gRPC status, or
> a retry decision, or a log line) belongs entirely to the outermost adapter — here, the
> controller's `switch` — and nowhere else. The same `NotFound` case, consumed by a queue worker
> instead of a controller, might mean "drop the message" or "dead-letter it"; it never means
> "404" in that context, because there is no HTTP response to produce. Keep the case types
> transport-agnostic so the Application layer stays usable from a worker, a CLI, or a gRPC service
> without modification.

### Shared case types are meaning-free: TransactionBehavior can't assume what a case means

`Error`, `NotFound`, `Success`, and the rest of this codebase's shared case types are plain,
meaning-free records. Any union is free to reuse `NotFound` to mean something that should
*commit*, or to introduce its own bespoke case type that should roll back — nothing about a shared
case type's identity says what it means for a given operation's transaction. That rules out
deciding commit-vs-rollback by pattern-matching case types against a fixed list (`response.Value is
Error or Failure or NotAuthorized or ValidationErrors or NotFound`): that's a closed-world
assumption baked into generic code, and it would silently misclassify any case type outside the
list, including one declared after the code that lists them was written.

The decision belongs on the union itself instead, via
[`ITransactionOutcome<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/ITransactionOutcome.cs):

```csharp
public interface ITransactionOutcome<TSelf> where TSelf : ITransactionOutcome<TSelf>
{
    static abstract bool ShouldCommit(TSelf response);
}
```

Each command's union implements it with a `switch` over **its own** cases:

```csharp
public union UpdateProductResult(Success, NotFound, ValidationErrors, Error, NotAuthorized)
    : IValidatable<UpdateProductResult>, ITransactionOutcome<UpdateProductResult>,
        IAuthorizable<UpdateProductResult>
{
    public static bool ShouldCommit(UpdateProductResult response) => response switch
    {
        Success => true,
        NotFound => false,
        ValidationErrors => false,
        Error => false,
        NotAuthorized => false,
    };
}
```

`TransactionBehavior` calls `TResponse.ShouldCommit(response)` and never inspects a case type by
name. Two compiler-enforced guarantees back this, not just convention:

- **Every case, including future ones, must be classified.** `ShouldCommit`'s `switch` is
  exhaustive over that union's own closed case set — exactly like every other union `switch` in
  this codebase. Adding a case to `UpdateProductResult` without updating its `ShouldCommit` fails
  the build with `CS8509`, the same diagnostic
  [`ExhaustivenessTests`](tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs) proves against
  a plain `switch`. Two more scratch projects,
  [`ShouldCommitNonExhaustive`](tests/CompileTimeChecks/ShouldCommitNonExhaustive) and
  [`ShouldCommitExhaustive`](tests/CompileTimeChecks/ShouldCommitExhaustive), prove this
  specifically for `ShouldCommit` using a union of entirely made-up case types —
  `Approved`, `Rejected`, `NeedsManualReview` — showing the guarantee holds for arbitrary case
  types, not just the ones any existing union happens to declare.
- **Only commands that need a transaction carry the obligation.** `ITransactionOutcome<TResponse>`
  is required by [`ITransactionalCommand<TResponse>`](src/MediatrUnionPoc.Application/Common/Abstractions/Messages.cs),
  not by `ICommand<TResponse>` itself — a command with nothing to commit or roll back (one that
  only publishes an event, say) is a plain `ICommand<TResponse>` and never needs to answer the
  question at all. A command that *does* declare `ITransactionalCommand<TResponse>` without its
  response implementing `ShouldCommit` is a compile error at the command declaration, not a
  pipeline behavior that quietly declines to run because a generic constraint wasn't satisfied. See
  [`NonTransactionalCommandTests`](tests/MediatrUnionPoc.Application.Tests/Behaviors/NonTransactionalCommandTests.cs)
  for both halves of this proved together.

[`TransactionBehaviorTests`](tests/MediatrUnionPoc.Application.Tests/Behaviors/TransactionBehaviorTests.cs)
proves the general case with an `ArbitraryOutcome` union whose case type (`SomeDevsOwnCaseType`)
shares no name, shape, or relationship with any case type used anywhere else in the codebase — the
behavior rolls back correctly purely by asking the union, never by recognizing the type.

### No exceptions for expected outcomes

A validation failure, a missing entity, an unauthorized caller, a business-rule violation — these
are not bugs, not infrastructure faults, and not exceptional. They are ordinary, anticipated
outputs of a use case, and this codebase treats them that way: every union declares them as cases,
and nothing in the request pipeline throws to signal one.

#### Why MediatR codebases reach for exceptions anyway

MediatR's own contract doesn't stop anyone from throwing: `IRequestHandler<TRequest,
TResponse>.Handle` returns `Task<TResponse>`, and nothing about that signature says a not-found or
a validation failure has to be part of `TResponse`. Three things push the ecosystem toward
exceptions as the default anyway:

- **ASP.NET Core ships a global exception-handling story, and no equivalent for anything else.**
  `UseExceptionHandler`/`IExceptionHandler` and problem-details middleware exist specifically to
  turn a thrown exception into an HTTP response in exactly one place, so `throw new
  NotFoundException(id)` inside any handler, anywhere, "just works" without that handler's author
  writing any HTTP-mapping code at all. There is no equivalent one-line convenience for "return a
  typed outcome and let something downstream map it" — that has to be built, which is exactly what
  the `switch` in every controller action in this repo is doing.
- **Before `union`, C# had no built-in way to say "returns exactly one of these N things."** The
  realistic options were a hand-rolled `Result<T>` type, a third-party library (`OneOf`,
  `ErrorOr`, `FluentResults`), or leaving `TResponse` a single DTO and using an exception for
  everything that isn't the happy path. The third option needs zero new types and zero new
  dependencies, which is why it's the default in countless tutorials, project templates, and
  production codebases — not because it's better, but because it requires nothing else to be
  installed.
- **It composes for free across call depth.** A handler three calls deep can `throw`, and nothing
  in between has to know or care — the exception unwinds the stack automatically until something
  catches it. A typed result has to be threaded back up explicitly through every intermediate
  return, which reads as more code to write in the moment it's written, even though it's the code
  that keeps the contract honest.

#### Why that convenience is a bad trade

None of the above makes exceptions-as-control-flow *correct* — it explains why it's common, not why
it's a good idea. **`throw` should mean what its name says: something exceptional happened.** The
moment a validation failure or a missing entity becomes a routine, expected outcome of calling an
operation, throwing for it stops being a shortcut and starts being a liability, in ways that get
worse as a codebase grows, not better:

- **It lies about the method's contract.** `Task<ProductDto> GetByIdAsync(ProductId id)` looks
  total — call it with any valid ID, get a `ProductDto` back — but if "not found" throws, the real
  contract is "returns a `ProductDto`, *or* throws one of an unbounded, undocumented set of
  exception types you'll only discover by reading the implementation or hitting one in production."
  A signature that misrepresents what can happen is worse than no signature at all, because it
  invites callers to trust it.
- **The compiler can't backstop it.** Forgetting `if (result is null)` is a mistake tooling can be
  made to catch — nullable reference types, or (better) exhaustive `switch` over a union with no
  null in its case set at all. Forgetting to wrap a call in `try`/`catch` for an exception type you
  didn't know it could throw is a mistake nothing catches — C# has no `throws` clause. The failure
  mode isn't "won't compile"; it's "compiles cleanly, throws unhandled in production."
- **It's measurably, not marginally, slower.** Throwing and catching a .NET exception costs orders
  of magnitude more than an ordinary return — stack-trace capture and stack unwinding are real CLR
  work that happens even when the `catch` is right there waiting. A `NotFound` that happens
  routinely (a stale bookmark, a race against a concurrent delete) shouldn't cost more than the
  `switch` that already exists to route it.
- **It pollutes observability with false signal.** Most hosting and APM setups treat any
  exception — logged or unhandled — as an *error*: it shows up in error-rate dashboards and can
  trip paging, for a stack trace nobody needed to explain a routine 404. Do this enough and a team
  either tunes out real incidents (alert fatigue — precisely the failure mode exceptions exist to
  prevent) or spends real effort teaching its own monitoring to ignore its own "errors," which is a
  worse position than never having generated the noise.
- **It scatters the outcome-to-response mapping instead of concentrating it.** With exceptions,
  "what HTTP status does a missing product produce" lives inside exception-handling middleware,
  switching on exception *type*, disconnected from the operation that threw it. With a union, that
  mapping lives in the one `switch` at the boundary that already has to exist for the happy path —
  there is no second, parallel place for it to drift out of sync with the first.
- **It's trivial to over-catch by accident.** A `catch (Exception)` written to handle one expected
  failure will just as happily catch a `NullReferenceException` from an actual bug, log it
  identically, and move on — nothing about the `catch` clause distinguishes "outcome I was
  expecting" from "bug I wasn't." A union case, by contrast, can only exist because some code
  explicitly constructed it as that case.

```mermaid
flowchart TB
    subgraph EX["Exception-based control flow (common default)"]
        direction TB
        E1["Handler.Handle(request)"] -->|"entity missing"| E2["throw NotFoundException(id)"]
        E2 --> E3["Stack unwinds through<br/>every layer on the way up"]
        E3 --> E4["Global exception middleware<br/>catches by exception TYPE"]
        E4 --> E5["Caller's signature never said<br/>this could happen"]
    end

    subgraph UN["Union-based control flow (this repo)"]
        direction TB
        U1["Handler.Handle(request)"] -->|"entity missing"| U2["return new NotFound(id)"]
        U2 --> U3["Ordinary return value —<br/>no stack unwinding"]
        U3 --> U4["Boundary switches<br/>by union CASE"]
        U4 --> U5["Caller's signature always said<br/>this could happen"]
    end
```

#### What this repo does instead

- **Exceptions are reserved for the truly exceptional** — a dropped DB connection, a bug, a
  contract violation from a dependency. Anything a handler can *expect* to happen is a union case,
  constructed and returned like any other value.
- **Impossible to "forget to catch."** A thrown `NotFoundException` three layers up is a runtime
  surprise if some caller doesn't wrap it. A `NotFound` case in a union is impossible to silently
  drop — the compiler makes every `switch` look at it.
- **Transactions decide by outcome, not by catching.** [`TransactionBehavior`](src/MediatrUnionPoc.Application/Common/Behaviors/TransactionBehavior.cs)
  asks the union itself — `ITransactionOutcome<TSelf>.ShouldCommit` — whether to commit, rather
  than inspecting which case type came back. No `catch` block is involved for expected outcomes; a
  `catch` still exists, but only for genuinely unexpected exceptions, and it rolls back and
  rethrows rather than swallowing anything into a result. See
  [Shared case types are meaning-free](#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means)
  for why this isn't a hardcoded list of "which case types mean error," and
  [Transactions: what rollback undoes, and why it matters](#transactions-what-rollback-undoes-and-why-it-matters)
  below for what "rollback" actually does.
- **Validator-owned members are deliberately not null-guarded; everything else is.** Every other
  public reference-type parameter in Application, Domain, Infrastructure and Api throws
  `ArgumentNullException` on `null`, but `CreateProductCommand.Name` and `UpdateProductCommand.Name`
  are not guarded, because `CreateProductValidator` / `UpdateProductValidator` own that input rule
  and report a `null` name as a `ValidationErrors` outcome. Over HTTP a `null` name is already
  rejected earlier by MVC model validation (400), so the guard would be redundant there; but a
  guard would make a direct `ISender.Send` caller get a thrown exception instead of the union case.
  Pinned by `ValidationBehaviorTests.Ctor_NullName_DoesNotThrowAndFailsValidation_Test`.

### Transactions: what rollback undoes, and why it matters

A database transaction groups a set of writes so they succeed or fail *together*. `TransactionBehavior`
opens one before a [transactional command's](#mediatr-vocabulary) handler runs, and decides after the
handler returns whether to **commit** (make every write inside it permanent) or **rollback** (undo
every write inside it, as if none of them had ever happened).

**Where this matters**: a handler that touches more than one repository, or writes to an entity
across more than one step, would otherwise risk a *partial write* — succeeding at step one and
failing at step two, leaving the database in a state no business rule ever intended to exist. A
transaction turns "did some of this succeed?" into a question that never needs asking: either the
whole unit of work happened, or none of it did.

**What actually gets undone**: everything the underlying `DbContext`'s change tracker recorded
during the handler's execution but never reached the database via `SaveChangesAsync` — inserts,
updates, and deletes alike. Nothing about this repo's own handler code has to remember what to
undo; the database transaction (plus `EfCoreUnitOfWork`'s change-tracker detach — see
[Notes and gotchas](#notes-and-gotchas)) does that bookkeeping.

**Why this is a benefit, not just a safety net**: it lets a handler write code that assumes success
and bail out cleanly on any unexpected outcome, without manually tracking "what have I already done
that I'd need to compensate for." Compare a hypothetical handler with no transaction: if it updated
one row, then failed validating a second write, undoing the first row's change would need to be
written by hand, remembered, and tested — a whole category of bugs a transaction eliminates by
construction.

This repo decides commit-vs-rollback by asking the union itself
(`ITransactionOutcome<TSelf>.ShouldCommit`, [above](#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means))
rather than by catching an exception — so rolling back a transaction is just as available for an
*expected*, successfully-returned outcome (`NotFound`, `ValidationErrors`) as it is for a genuine
fault, with no `catch` block required to trigger it. See
[Commit vs. rollback, message by message](#commit-vs-rollback-message-by-message) below for this
traced through a concrete request.

### Unit of Work: one session, every repository

[`IUnitOfWork`](src/MediatrUnionPoc.Domain/IUnitOfWork.cs) is registered once per request (a scoped
[dependency injection](#cross-cutting-concepts) lifetime) and handed to every repository and
handler that needs it during that request — which means every repository sharing that same
`IUnitOfWork` is also sharing the same underlying `DbContext`/change-tracking session. A handler
that needs `IProductRepository` *and* a hypothetical `IOrderRepository` in the same operation gets
both backed by the same session automatically, purely from how dependency injection resolves scoped
services — no extra wiring required to keep them consistent with each other.

This is what lets `IUnitOfWork.CommitAsync()`/`RollbackAsync()` cover *every* repository touched
during the request with one call, instead of each repository having to save (or undo) its own
changes independently. That's the specific gap a **standalone repository or domain service method**
doesn't close on its own: a repository's own save method only knows about *its* entities —
coordinating a write that spans two repositories would need either the repositories to reference
each other (leaking persistence details across an otherwise clean boundary) or some other component
to explicitly own "did both succeed." [Unit of Work](#architectural-patterns) is that "other
component" — it doesn't replace repositories or domain services, and a simple operation touching
exactly one repository doesn't need to reach for it: a `Product`-only handler here still calls
`IProductRepository` directly, with `IUnitOfWork` only mattering for its transaction boundary, not
as a required intermediary for every read or write.

### Vogen: avoiding primitive obsession

> [!TIP]
> Deeper, citation-backed research behind this section lives in
> [`docs/research/vogen-research.md`](docs/research/vogen-research.md) — primary sources (Vogen's
> README and official docs site) for the factory/equality/conversion behavior described below, and
> the defense-in-depth reasoning behind the EF Core converter choice.

[Vogen](https://github.com/SteveDunn/Vogen) is a [source generator](#c-language-concepts) that
turns a bare primitive (`Guid`, `decimal`, `string`, ...) into a distinct, validated
[value type](#vogen-vocabulary) — so `ProductId` and an unrelated `Guid` parameter can never be
swapped by mistake, and an invalid value can never be constructed in the first place. This section
covers a supporting library this repo happens to use for that concern, not a language feature the
union pattern itself depends on — a different implementation could use plain primitives, a
different value-object library, or hand-rolled wrapper types instead.

```csharp
[ValueObject<Guid>(conversions: Conversions.SystemTextJson)]
public readonly partial struct ProductId
{
    private static Validation Validate(Guid input) =>
        input != Guid.Empty ? Validation.Ok : Validation.Invalid("ProductId cannot be an empty guid.");

    public static ProductId New() => From(Guid.NewGuid());
}
```

From this ~6-line declaration, Vogen generates:

- **Factory methods** — `ProductId.From(guid)` (throws on invalid input) and `TryFrom(guid, out id)`
  (doesn't). `Validate` runs inside *every* generated factory, so an empty-guid `ProductId` cannot
  exist anywhere in the codebase — not just at the API boundary where FluentValidation already
  checks it.
- **Structural equality, hashing, and `ToString`** — two `ProductId`s wrapping the same `Guid` are
  equal; [`record`](#c-language-concepts)-style value semantics without needing `record` (this has
  to be a `struct` per Vogen's own constraints, and `readonly partial struct` is the declaration
  shape it generates into).
- **Conversions**, opt-in per flag on the attribute. This repo uses `Conversions.SystemTextJson`
  only, so `ProductId` serializes as a bare GUID string (`"..."`), not as a wrapper object — see
  [`ProductDto`](src/MediatrUnionPoc.Application/Features/Products/Common/ProductDto.cs) for where
  that matters on the wire.

`Money` ([`Money.cs`](src/MediatrUnionPoc.Domain/Money.cs)) follows the identical pattern over
`decimal`, rejecting negative amounts.

Two Vogen-specific pitfalls we hit while building this — full detail in
[Notes and gotchas](#notes-and-gotchas) — are worth knowing about up front if you add your own
value object: don't set `PrivateAssets="all"` on the `Vogen` package reference, and don't reach
for Vogen's generated `EfCoreValueConverter` from a persistence-agnostic Domain project (this repo
hand-writes its EF Core converters in Infrastructure instead — see
[`ValueConverters.cs`](src/MediatrUnionPoc.Infrastructure/ValueConverters.cs)).

## Adding a new command or query

MediatR is an in-process [mediator](#architectural-patterns): instead of a controller calling a
service directly, it sends a message object and MediatR routes it to exactly one handler. This is
what makes CQRS's "Commands write, Queries read" split easy to enforce — commands and queries are
just different message types, and cross-cutting concerns (logging, validation, transactions) wrap
around all of them uniformly via **[pipeline behaviors](#architectural-patterns)**, the MediatR
equivalent of ASP.NET Core middleware.

> [!TIP]
> Deeper, citation-backed research behind MediatR's pipeline mechanics lives in
> [`docs/research/mediatr-research.md`](docs/research/mediatr-research.md) — primary sources
> (MediatR's own repo and release notes), plus a worked sequence diagram tracing
> `CreateProductCommand` through the full pipeline.

```mermaid
flowchart LR
    Controller -->|"sender.Send(request)"| Behaviors["Pipeline behaviors\n(run in registration order)"]
    Behaviors --> Handler["IRequestHandler&lt;TRequest,TResponse&gt;"]
    Handler --> Behaviors
    Behaviors --> Controller
```

Every operation in this repo has five pieces. Using a hypothetical `Ping` query as a minimal,
non-Product example:

**1. Declare what can come back, as a union:**

```csharp
public union PingResult(PongDto, Error) : IValidatable<PingResult>
{
    public static PingResult FromValidationErrors(ValidationErrors errors) =>
        new Error(errors.ToErrorMessage(), Error.ValidationFailureCode);
}
```

**2. Declare the request.** Implement `IQuery<TResponse>` for a read-only request (this example),
`ICommand<TResponse>` for one that mutates state but needs no transaction, or
`ITransactionalCommand<TResponse>` for one `TransactionBehavior` should commit or roll back —
the latter requires the response union to implement `ITransactionOutcome<TResponse>`:

```csharp
public sealed record PingQuery(string Message) : IQuery<PingResult>;
```

**3. (Optional) add a [FluentValidation](https://github.com/FluentValidation/FluentValidation)
validator** — picked up automatically by assembly scanning, no manual registration needed.
FluentValidation is this POC's illustrative choice for wiring validation, not a prescription — any
approach that can short-circuit into the response union via `IValidatable<TSelf>` fits the pattern
equally well.

> [!TIP]
> Deeper, citation-backed research on FluentValidation's async/cascade/DI behavior in this
> pipeline lives in
> [`docs/research/fluentvalidation-research.md`](docs/research/fluentvalidation-research.md).

```csharp
public sealed class PingValidator : AbstractValidator<PingQuery>
{
    public PingValidator() => RuleFor(x => x.Message).NotEmpty();
}
```

**4. Write the handler** — return a case type; MediatR/the union's implicit conversion does the
rest:

```csharp
public sealed class PingHandler : IRequestHandler<PingQuery, PingResult>
{
    public Task<PingResult> Handle(PingQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<PingResult>(new PongDto(request.Message));
}
```

**5. Call it from a controller** and `switch` exhaustively on the result:

```csharp
var result = await sender.Send(new PingQuery("hi"), cancellationToken);

return result switch
{
    PongDto pong => Ok(pong),
    Error error => Problem(detail: error.Message, statusCode: 500, title: error.Code),
};
```

That's it — no DI registration step for the handler or validator; both are found via
`services.AddMediatR(...)` and `services.AddValidatorsFromAssembly(...)` in
[`DependencyInjection.cs`](src/MediatrUnionPoc.Application/DependencyInjection.cs).

## Request lifecycle

```mermaid
flowchart TD
    Client([HTTP request]) --> Controller[ProductsController]
    Controller --> Sender["sender.Send(request)"]
    Sender --> Logging[LoggingBehavior]
    Logging --> Validation{FluentValidation passes?}
    Validation -->|No| BuildErrors["TResponse.FromValidationErrors(...)"]
    BuildErrors --> ReturnUp[Union response]
    Validation -->|Yes| IsCommand{Command or query?}
    IsCommand -->|Query| Handler[Feature handler]
    IsCommand -->|Command| Begin[TransactionBehavior: begin]
    Begin --> Handler
    Handler --> Repo[(Repository / EF Core)]
    Repo --> Handler
    Handler --> Outcome{Union case returned}
    Outcome -->|Success or DTO| Commit[TransactionBehavior: commit]
    Outcome -->|NotFound, Error, Failure, NotAuthorized, or ValidationErrors| Rollback[TransactionBehavior: rollback]
    Commit --> ReturnUp
    Rollback --> ReturnUp
    ReturnUp --> Map{Controller switches on the union}
    Map -->|Dto| Ok200[200 OK / 201 Created]
    Map -->|NotFound| NF404[404 Not Found]
    Map -->|ValidationErrors| BadRequest400[400 Bad Request]
    Map -->|Error| ServerError500[500 Internal Server Error]
    Ok200 --> Done([HTTP response])
    NF404 --> Done
    BadRequest400 --> Done
    ServerError500 --> Done
```

Note that only the last "Controller switches on the union" step is HTTP-aware — everything above
it deals purely in domain outcomes.

### Commit vs. rollback, message by message

```mermaid
sequenceDiagram
    participant C as Controller
    participant P as Pipeline (Logging, Validation, Transaction)
    participant H as Handler
    participant U as IUnitOfWork
    participant D as Database

    C->>P: Send(DeleteProductCommand)
    P->>U: BeginTransactionAsync()
    P->>H: Handle(command)
    H->>D: GetByIdAsync(id)
    D-->>H: Product or null

    alt product found
        H-->>P: DeleteProductResult(Success)
        P->>U: CommitAsync()
        U->>D: SaveChangesAsync()
    else product missing
        H-->>P: DeleteProductResult(NotFound)
        P->>U: RollbackAsync()
        U--xD: no SaveChangesAsync call
    end

    P-->>C: DeleteProductResult
```

No `catch` block appears anywhere in this flow — the branch is decided entirely by which case type
the handler returned.

## Authorization

This POC demonstrates two authorization shapes side by side, both built from **standard ASP.NET
Core authorization primitives** (`IAuthorizationService`, `IAuthorizationRequirement`,
`IAuthorizationHandler`, named policies) and both converging on the same union-based outcome —
`NotAuthorized` as just another case, never an exception:

- **Role/policy-based**, checked *before* a handler runs, by a MediatR pipeline behavior — nothing
  about the request's payload matters, only who's calling. Demoed by `DeleteProductCommand`'s
  `Administrator` policy: only an administrator may delete a product.
- **Resource-based**, checked *inside* a handler, once it has loaded the specific resource being
  acted on. Demoed by `UpdateProductCommand`'s `ProductOwner` policy: only the product's owner may
  update it.

> [!NOTE]
> Both are *example configurations* of a general mechanism, not the only valid way to wire
> authorization and not a prescription that every command needs one of them — a different project
> might gate different operations, use different policies, combine styles differently, or skip
> authorization entirely for commands that don't need it.

### Why two different points in the request lifetime

A role/policy check like "is this caller an administrator?" only needs the caller's
`ClaimsPrincipal` — nothing about the request's payload matters, so it can run generically in a
pipeline behavior before the handler, the same way `ValidationBehavior` runs before the handler.

A resource-based check like "does this caller own *this* product?" can't run that early: there is
no product to check ownership against until a handler has loaded it from the repository. ASP.NET
Core's own resource-based authorization guidance is explicit about this — resource-based checks
are necessarily imperative, called from inside the code that already has the resource in hand,
rather than declared ahead of time the way `[Authorize]` or a pipeline behavior can. This isn't a
gap in this repo's pipeline; it's why `ResourceAuthorizationService` exists as something a handler
calls explicitly instead of something wired into `AddTransient(typeof(IPipelineBehavior<,>), ...)`
alongside the other behaviors.

### How the two flows fit together

```mermaid
flowchart TD
    subgraph RoleBased["Role-based — pre-handler (DeleteProductCommand)"]
        direction LR
        C1["Controller"] -->|"sender.Send(request)"| L1[LoggingBehavior]
        L1 --> A1{"AuthorizationBehavior:\nnamed policy?"}
        A1 -->|No| N1["TResponse.FromNotAuthorized(...)"]
        A1 -->|Yes| V1[ValidationBehavior] --> T1[TransactionBehavior] --> H1[Handler]
    end

    subgraph ResourceBased["Resource-based — inside the handler"]
        direction LR
        C2["Controller"] -->|"sender.Send(UpdateProductCommand)"| L2[LoggingBehavior]
        L2 --> V2[ValidationBehavior] --> T2[TransactionBehavior] --> H2["UpdateProductHandler"]
        H2 --> G2{"repository.GetByIdAsync(id)"}
        G2 -->|"found"| R2{"ResourceAuthorizationService.AuthorizeAsync:\nProductOwner policy?"}
        R2 -->|No| N2["UpdateProductResult.FromNotAuthorized(...)"]
        R2 -->|Yes| U2["product.UpdateDetails(...)"]
    end

    N1 --> Conv(["IAuthorizable&lt;TSelf&gt;.FromNotAuthorized(NotAuthorized)"])
    N2 --> Conv
    Conv --> Map{"Controller switches on\nthe union result"}
    Map -->|NotAuthorized| Forbidden403[403 Forbidden]
```

Every path calls `IAuthorizationService` under the hood and ends up asking the response union's
`IAuthorizable<TSelf>.FromNotAuthorized(...)` to build the same shared `NotAuthorized` case — only
*where* in the request's lifetime that call happens differs.

### Role-based: `IRequiresAuthorization` + `AuthorizationBehavior`

1. **[`IRequiresAuthorization`](src/MediatrUnionPoc.Application/Common/Abstractions/IRequiresAuthorization.cs)**
   — a request implements this, exposing `ClaimsPrincipal Principal` and a `string PolicyName` to
   evaluate it against, to opt into `AuthorizationBehavior`. A request that doesn't implement it
   simply doesn't match the behavior's generic constraints and skips this check entirely — the
   same opt-in pattern `ITransactionalCommand` uses for `TransactionBehavior`.
2. **[`IAuthorizable<TSelf>`](src/MediatrUnionPoc.Application/Common/Abstractions/IAuthorizable.cs)**
   — a response union implements this (`static abstract TSelf FromNotAuthorized(NotAuthorized)`)
   so the behavior can build the union's `NotAuthorized` case generically, the same role
   `IValidatable<TSelf>` plays for `ValidationErrors`. Both authorization patterns here rely
   on this same interface.
3. **[`AuthorizationBehavior<TRequest,TResponse>`](src/MediatrUnionPoc.Application/Common/Behaviors/AuthorizationBehavior.cs)**
   — calls `IAuthorizationService.AuthorizeAsync(request.Principal, request.PolicyName)` (the
   policy-only, two-argument overload), reading the policy name generically off the request rather
   than hardcoding one. On failure, it short-circuits to `TResponse.FromNotAuthorized(...)`
   without ever calling the handler.
4. **[`AdministratorRequirement`](src/MediatrUnionPoc.Application/Common/Authorization/AdministratorRequirement.cs)
   and [`AdministratorAuthorizationHandler`](src/MediatrUnionPoc.Application/Common/Authorization/AdministratorAuthorizationHandler.cs)**
   — a real `IAuthorizationRequirement`/`IAuthorizationHandler<TRequirement>` pair, modeled
   directly on ASP.NET Core's own built-in `RolesAuthorizationRequirement`/`RolesAuthorizationHandler`:
   the requirement carries a set of allowed roles, and the handler succeeds if the caller is in
   *any one* of them (an empty set is automatically satisfied — nothing to challenge against).
5. **Registration**, in [`Application/DependencyInjection.cs`](src/MediatrUnionPoc.Application/DependencyInjection.cs)'s
   `AddApplication()`:

   ```csharp
   services.AddAuthorizationCore(options =>
       options.AddPolicy(
           AuthorizationPolicies.Administrator,
           policy => policy.Requirements.Add(new AdministratorRequirement(AuthorizationRoles.Administrator))));
   services.AddSingleton<IAuthorizationHandler, AdministratorAuthorizationHandler>();
   ```

   `AddAuthorizationCore` (not `AddAuthorization`) is deliberate: it registers the authorization
   *service and policy evaluation* without pulling in ASP.NET Core's `[Authorize]`
   attribute/middleware machinery, which this POC has no use for — `AuthorizationBehavior` calls
   `IAuthorizationService` directly instead of relying on an HTTP-pipeline gate.

> [!NOTE]
> `DeleteProductCommand` is this pattern's production consumer: it implements
> `IRequiresAuthorization` with `PolicyName => AuthorizationPolicies.Administrator`, so
> `AuthorizationBehavior` rejects a non-administrator before `DeleteProductHandler` ever runs. The
> mechanism is also covered independently of that command by
> [`AuthorizationBehaviorTests`](tests/MediatrUnionPoc.Application.Tests/Behaviors/AuthorizationBehaviorTests.cs)'s
> `ArbitraryAdminCommand` fixture.

### Resource-based: `ResourceAuthorizationService` + `OwnerAuthorizationHandler<TResource>`

1. **[`IOwnedResource`](src/MediatrUnionPoc.Application/Common/Authorization/IOwnedResource.cs)**
   — any resource shape exposing `string OwnerId`, independent of the resource's own domain type.
   [`OwnedProductResource`](src/MediatrUnionPoc.Application/Features/Products/Common/OwnedProductResource.cs)
   adapts an already-loaded `Product` to it — `Product` itself can't implement `IOwnedResource`
   directly, since Domain must not depend on Application.
2. **[`OwnerAuthorizationHandler<TResource>`](src/MediatrUnionPoc.Application/Common/Authorization/OwnerAuthorizationHandler.cs)**
   — generic over any `IOwnedResource`, registered against the two-generic-parameter
   `AuthorizationHandler<TRequirement, TResource>` form (which receives the loaded resource
   directly), unlike the one-generic-parameter form the role-based handler above uses. It succeeds
   an `OperationAuthorizationRequirement` when the caller's `ClaimTypes.NameIdentifier` claim
   matches the resource's `OwnerId`, ignoring the requirement's `Name` entirely — so the same
   registered handler instance can answer every CRUD-shaped operation for `TResource` without a
   bespoke requirement type per operation.
3. **[`ResourceAuthorizationService`](src/MediatrUnionPoc.Application/Common/Authorization/ResourceAuthorizationService.cs)**
   — the resource-based counterpart to `AuthorizationBehavior`, callable from inside a handler
   once it has loaded the resource. It calls `IAuthorizationService`'s resource-aware
   three-argument `AuthorizeAsync(principal, resource, policyName)` overload — not the
   policy-only overload `AuthorizationBehavior` uses. It deliberately stops short of building the
   union's `NotAuthorized` case itself (that needs `IAuthorizable<TSelf>` and the concrete union
   type, which only the calling handler knows); it returns a plain `NotAuthorized?` instead, for
   the handler to pass straight to `TResponse.FromNotAuthorized(...)`.
4. **[`UpdateProductHandler`](src/MediatrUnionPoc.Application/Features/Products/Update/UpdateProductHandler.cs)**
   — loads the product, then calls
   `resourceAuthorizationService.AuthorizeAsync(request.Principal, OwnedProductResource.FromDomain(product), AuthorizationPolicies.ProductOwner, cancellationToken)`,
   returning `UpdateProductResult.FromNotAuthorized(notAuthorized)` on failure before ever calling
   `product.UpdateDetails(...)`. `UpdateProductCommand` deliberately does **not** implement
   `IRequiresAuthorization` — that pipeline path runs before any resource is loaded, too early for
   an ownership check.
5. **Registration**, in the same `AddApplication()`:

   ```csharp
   options.AddPolicy(
       AuthorizationPolicies.ProductOwner,
       policy => policy.Requirements.Add(AuthorizationOperations.Update));
   services.AddSingleton<IAuthorizationHandler, OwnerAuthorizationHandler<OwnedProductResource>>();
   services.AddScoped<ResourceAuthorizationService>();
   ```

> [!WARNING]
> `OwnerAuthorizationHandler<OwnedProductResource>` is registered as a **singleton** because it
> has no dependency of its own — it only reads claims off the `ClaimsPrincipal` and compares a
> string. That registration is only safe *because* of that. A resource handler that instead needs
> to depend on EF Core (say, to re-check an owner against the database rather than trusting the
> already-loaded resource) must **not** be registered as a singleton — `DbContext` and other
> scoped EF Core services aren't safe to share across requests the way a singleton would; register
> a handler like that scoped or transient instead.

### Zero-to-many handlers, and multiple requirements

None of the policies above are special-cased by this repo — every behavior described here is
native `IAuthorizationService` behavior:

- A policy can hold **multiple requirements**; `AuthorizeAsync` only succeeds if *every*
  requirement succeeds (AND across requirements).
- A single requirement type can have **zero, one, or many registered handlers** (there is no
  1:1 requirement-to-handler constraint); a requirement succeeds if *any one* of its handlers
  calls `context.Succeed(requirement)` (OR across handlers) — the same "any match is enough"
  shape `AdministratorAuthorizationHandler` already applies *within* a single handler across
  multiple allowed roles, just one level up, across handlers..

[`ResourceAuthorizationOrAcrossHandlersTests`](tests/MediatrUnionPoc.Application.Tests/Authorization/ResourceAuthorizationOrAcrossHandlersTests.cs)
exercises this generically (multiple handlers registered for the same requirement type, only one
of which succeeds) to prove it's real `IAuthorizationService` behavior this repo relies on, not
something reimplemented here.

### Where the identity comes from

This POC has no real authentication — no login, no JWTs, no cookies. Instead,
[`ProductsController`](src/MediatrUnionPoc.Api/Controllers/ProductsController.cs) builds a
`ClaimsPrincipal` from two request headers:

- **`X-Admin`** — a value of `"true"` (case-insensitive) adds an `Administrator` role claim, read
  by `DeleteAsync` to satisfy the `Administrator` policy.
- **`X-Caller-Id`** — its value becomes the caller's `ClaimTypes.NameIdentifier` claim, read by
  `CreateAsync` (to set the new product's owner) and `UpdateAsync` (to prove ownership).

```csharp
private static ClaimsPrincipal CallerPrincipal(string? adminHeader, string? callerIdHeader)
{
    var identity = new ClaimsIdentity(authenticationType: "Header");

    if (string.Equals(adminHeader, "true", StringComparison.OrdinalIgnoreCase))
    {
        identity.AddClaim(new Claim(ClaimTypes.Role, AuthorizationRoles.Administrator));
    }

    if (!string.IsNullOrEmpty(callerIdHeader))
    {
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, callerIdHeader));
    }

    return new ClaimsPrincipal(identity);
}
```

Try both against a running instance (`dotnet run --project src/MediatrUnionPoc.Api`):

```bash
# Resource-based (UpdateProductCommand, ProductOwner policy)

# Create as caller "alice" — she becomes the product's owner
curl -i -X POST https://localhost:<port>/api/products \
  -H "X-Caller-Id: alice" -H "Content-Type: application/json" \
  -d '{"name":"Widget","price":9.99}'

# 403 Forbidden — "bob" didn't create this product
curl -i -X PUT https://localhost:<port>/api/products/<id> \
  -H "X-Caller-Id: bob" -H "Content-Type: application/json" \
  -d '{"name":"Widget v2","price":12.99}'

# 204 No Content — "alice" owns this product
curl -i -X PUT https://localhost:<port>/api/products/<id> \
  -H "X-Caller-Id: alice" -H "Content-Type: application/json" \
  -d '{"name":"Widget v2","price":12.99}'
```

```bash
# Role-based (DeleteProductCommand, Administrator policy)

# 403 Forbidden — no administrator identity
curl -i -X DELETE https://localhost:<port>/api/products/<id>

# 204 No Content — claims Administrator
curl -i -X DELETE https://localhost:<port>/api/products/<id> -H "X-Admin: true"
```

> [!WARNING]
> `X-Admin` and `X-Caller-Id` are stand-ins for real authentication, appropriate only for this
> POC. A real deployment would replace `CallerPrincipal(...)` with `HttpContext.User` — populated
> by an actual authentication scheme (cookies, JWT bearer, etc.) via `app.UseAuthentication()` —
> and delete the header-reading code entirely. `AuthorizationBehavior`, `ResourceAuthorizationService`,
> `IRequiresAuthorization`, and `IAuthorizable<TSelf>` wouldn't need to change at all: they only
> ever see a `ClaimsPrincipal`, never how it was constructed.

### Configuring role-based authorization for a new command

To gate another command purely by role, the way `DeleteProductCommand` is gated:

1. Add `ClaimsPrincipal Principal` to the command and implement `IRequiresAuthorization`,
   returning the name of whichever registered policy should gate it from `PolicyName`
   (`AuthorizationPolicies.Administrator` to reuse the existing one).
2. Add `NotAuthorized` to the response union's case list and implement `IAuthorizable<TSelf>`
   (`FromNotAuthorized(NotAuthorized) => notAuthorized;` is usually the whole implementation).
3. If the union also implements `ITransactionOutcome<TSelf>`, add a `NotAuthorized => false` arm
   to its `ShouldCommit` switch — the compiler enforces this, the same way it enforces every other
   case being classified.
4. Have the controller build (or reuse) a `ClaimsPrincipal` and pass it on the command; add a
   `NotAuthorized` arm to the controller's `switch`, mapping it to `403 Forbidden`.

To require a *different* role than `Administrator` for some other operation, register a new named
policy with its own `AdministratorRequirement("SomeOtherRole")` (or a differently-named
role-requirement type, if `Administrator` shouldn't be in its allowed-roles list at all), then
return that policy's name from the new command's `PolicyName` — `AuthorizationBehavior` doesn't
care which policy a request names, only that one is registered under that name. The
requirement/handler pair already supports multiple allowed roles per policy and an
any-one-matches check, so a single policy can also gate on more than one role
(`new AdministratorRequirement("Administrator", "SuperUser")`) without a new handler.

### Configuring resource-based authorization for a new command

To gate another command the way `UpdateProductCommand` is gated (ownership-only):

1. Add `ClaimsPrincipal Principal` to the command, but do **not** implement `IRequiresAuthorization`
   on it — the check happens inside the handler, not the pipeline.
2. Make sure the resource being acted on implements (or is adapted to, the way
   `OwnedProductResource` adapts `Product`) `IOwnedResource`, or define a new resource-marker
   interface if the check isn't ownership-shaped.
3. Register a policy backed by an `OperationAuthorizationRequirement` (or a custom requirement),
   and a handler for it — `OwnerAuthorizationHandler<TResource>` can be reused directly if the
   resource already implements `IOwnedResource`.
4. Inject `ResourceAuthorizationService` into the handler; after loading the resource, call
   `AuthorizeAsync(request.Principal, resource, policyName, cancellationToken)` and return
   `TResponse.FromNotAuthorized(notAuthorized)` when it comes back non-null.
5. Add `NotAuthorized` to the response union's case list and implement `IAuthorizable<TSelf>`, the
   same as the role-based case above — both patterns converge on this same interface.
6. Add a `NotAuthorized` arm to the controller's `switch`, mapping it to `403 Forbidden`.
### Why not `IAuthorizationRequirementData` attributes

.NET 11 widens `IAuthorizationRequirementData`-backed attribute authorization (declaring
requirements via attributes ASP.NET Core discovers automatically) to cover MVC controllers, not
just Minimal APIs — and `ProductsController` is an MVC controller. This repo doesn't use it
because that feature is *declarative*, discovered at the HTTP endpoint layer (an attribute on an
action or controller drives the check before the action body runs). This repo's authorization runs
one layer down, in the Application layer: `AuthorizationBehavior` is a MediatR pipeline behavior
keyed off the request type, and `ResourceAuthorizationService` is called directly from inside a
handler. Neither has an HTTP action to attach a discoverable attribute to — there's no
endpoint-attribute-discovery step in this repo's authorization path for that feature to plug into.

## Worked example: `UpdateProductCommand`, case by case

The diagrams above show the pipeline shape in the abstract. This one traces a single, concrete
request — `PUT /api/products/{id}` — all the way through, branching at every point where a
different case of [`UpdateProductResult`](src/MediatrUnionPoc.Application/Features/Products/Update/UpdateProductResult.cs)
(`union(Success, NotFound, ValidationErrors, Error, NotAuthorized)`) could come back. Each terminal
branch is tagged with the case type it produces («Success», «NotFound», «ValidationErrors»,
«Error», «NotAuthorized») and color-coded so the same case is easy to follow from where it's
created to the HTTP status it becomes.

Four of the five cases are actually reachable from
[`UpdateProductHandler`](src/MediatrUnionPoc.Application/Features/Products/Update/UpdateProductHandler.cs)
as written today, including the resource-based `ProductOwner` check described in
[Authorization](#authorization) above. `«Error»` is included because the union *declares* it as a
possible outcome — reserved for a future unexpected-failure path — even though nothing in the
current handler produces it; the diagram marks that branch as dashed for exactly this reason.

```mermaid
flowchart TD
    classDef success fill:#d8f5d0,stroke:#2f9e44,stroke-width:2px;
    classDef notfound fill:#fff3bf,stroke:#e8590c,stroke-width:2px;
    classDef validation fill:#ffe3e3,stroke:#c92a2a,stroke-width:2px;
    classDef error fill:#f1f3f5,stroke:#495057,stroke-width:2px,stroke-dasharray: 4 3;
    classDef notauthorized fill:#e5dbff,stroke:#7048e8,stroke-width:2px;

    Client(["PUT /api/products/{id}<br/>body: name, price"]) --> Ctrl["ProductsController.Update"]
    Ctrl --> Build["new UpdateProductCommand(id, name, price)"]
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

    Own -->|"yes"| Update["product.UpdateDetails(name, price)"]
    Update --> Ok["«Success»<br/>new Success()"]:::success
    Ok --> Commit["TransactionBehavior<br/>CommitAsync() then SaveChangesAsync()"]:::success
    Commit --> Log2c["LoggingBehavior<br/>log: result = Success"]:::success
    Log2c --> Map3["controller switch"]:::success
    Map3 --> R204["204 No Content"]:::success

    Handle -.->|"declared, not exercised today"| Err["«Error»<br/>new Error(message, code)"]:::error
    Err -.-> Roll2["TransactionBehavior<br/>RollbackAsync()"]:::error
    Roll2 -.-> Log2d["LoggingBehavior<br/>log: result = Error"]:::error
    Log2d -.-> Map4["controller switch"]:::error
    Map4 -.-> R500["500 Internal Server Error"]:::error
```

Reading the diagram:

- **Green («Success»)** is the only path where `TransactionBehavior` commits — everything else
  rolls back or never opens a transaction at all.
- **Red («ValidationErrors»)** short-circuits *before* `TransactionBehavior` even runs — no
  transaction is opened for input that never should have reached the handler.
- **Yellow («NotFound»)**, **purple («NotAuthorized»)**, and **dashed grey («Error»)** all reach
  the handler, open a transaction, and get rolled back — the difference between them is only which
  case type the handler chose to return, not any `try`/`catch` structure.
- **Purple («NotAuthorized»)** is the one branch that depends on a second lookup beyond the
  entity's existence — the caller's identity has to match the product's owner, not just the
  product having to exist — which is why it's checked only after `Lookup` already succeeded, the
  same ordering [Authorization](#authorization) describes for resource-based checks generally.
- Every branch still passes back through `LoggingBehavior` on the way out, so every outcome —
  success or not — gets logged exactly once, symmetrically.

## Extending the pattern: syncing a search index

Not implemented in this repo, but a natural extension once every command already reports its
outcome as a union: keeping a search index (Elasticsearch, in this example) in sync with the write
model whenever a product is created, updated, or deleted — without threading Elasticsearch calls
through every handler's own business logic.

**The mechanism**: MediatR is also a **notification** publisher, not just a request/response
mediator — `IPublisher.Publish(new ProductCreated(id))` fans a message out to *every* registered
`INotificationHandler<ProductCreated>`, zero-or-many, none of them able to affect the original
command's own result. That's the right shape here: syncing a search index is a side effect of a
successful write, not part of *deciding* whether the write succeeded, so it shouldn't be able to
turn a `Success` into anything else.

1. **Publish after `TransactionBehavior` commits, not from inside the handler.** A notification
   raised from inside `CreateProductHandler`, before the transaction commits, could fire for a
   write that later rolls back — the search index would then reference a product the database
   never actually kept. Publishing needs to happen only on the `Success` branch, after
   `CommitAsync()`.
2. **Add a `Success`-shaped [domain event](#cross-cutting-concepts) per operation** —
   `ProductCreated(ProductId, Name, Price)`, `ProductUpdated(ProductId, Name, Price)`,
   `ProductDeleted(ProductId)` — each just data, no behavior.
3. **A dedicated `INotificationHandler<ProductCreated>` (etc.) owns the Elasticsearch write** —
   indexing a new document, updating an existing one, or deleting one, entirely separate from
   `CreateProductHandler`/`UpdateProductHandler`/`DeleteProductHandler`, which never need to know a
   search index exists at all.

```mermaid
sequenceDiagram
    participant C as Controller
    participant P as Pipeline (incl. TransactionBehavior)
    participant H as CreateProductHandler
    participant U as IUnitOfWork
    participant M as IPublisher
    participant S as ProductCreated handler
    participant ES as Elasticsearch

    C->>P: Send(CreateProductCommand)
    P->>H: Handle(command)
    H-->>P: CreateProductResult(ProductDto)
    P->>U: CommitAsync()
    U-->>P: committed
    P->>M: Publish(new ProductCreated(id, name, price))
    P-->>C: CreateProductResult
    M->>S: Handle(ProductCreated)
    S->>ES: Index document
```

Note the last two arrows happen *after* the controller already has its response — indexing is
fire-and-forget from the caller's point of view, and a slow or even temporarily-failing
Elasticsearch write never adds latency to the product-creation request itself, nor can it change
the `201 Created` the caller already received. That's an intentional trade: the search index
becomes eventually consistent[^eventual-consistency] with the write model rather than immediately
consistent — the right trade for a search index, and the wrong one for, say, an inventory count a
checkout flow depends on.

> [!TIP]
> This same shape generalizes to any side effect that shouldn't block or influence a command's own
> result — an audit log, a cache invalidation, an outbound webhook, an email notification. Add a
> notification, publish it after commit, and let as many independent handlers subscribe as needed.

## Speculative shared case types for a larger API

Not implemented here, but worth having in your vocabulary for a real project — none of these map
1:1 onto an HTTP status, deliberately. Like the shared case types actually used in this repo, each
one below is meant to be a meaning-free record reused across many unions, not tied to any single
operation:

| Case                                    | Meaning                                                                 |
| ----------------------------------------- | -------------------------------------------------------------------------- |
| `Accepted(jobId)`                        | Work was queued/deferred, not completed synchronously                     |
| `Conflict(currentVersion)`               | [Optimistic-concurrency](#cross-cutting-concepts) version mismatch on update |
| `Locked(heldBy)`                         | Resource is [pessimistically locked](#cross-cutting-concepts) by another process |
| `RateLimited(retryAfter)`                | Caller hit a throttling limit                                             |
| `Timeout(dependency)`                    | A downstream dependency didn't respond in time                           |
| `QuotaExceeded(limit, current)`          | A business quota (not a rate limit) was exceeded                          |
| `PartialSuccess(succeeded, failed)`      | A batch operation partially completed                                     |
| `AlreadyProcessed(idempotencyKey)`       | A duplicate request was detected and safely [ignored](#cross-cutting-concepts) — see **Idempotency** |
| `RequiresConfirmation(prompt)`           | The action needs an explicit second confirmation before proceeding        |
| `Stale(asOf)`                            | Data was served from a cache/read-replica and may be out of date          |
| `Deprecated(replacement)`                | The operation still works but callers should migrate                      |

A queue consumer, a scheduled job, and an HTTP controller could all share the exact same
`Conflict`/`Locked`/`AlreadyProcessed` cases and each map them to something completely different
in their own boundary code.

## Notes and gotchas

- **Vogen + `PrivateAssets="all"`:** don't set this on the `Vogen` package reference. Vogen ships
  a small runtime support assembly (`Vogen.SharedTypes.dll`) alongside its source generator;
  `PrivateAssets="all"` strips that runtime assembly from consuming projects too, causing a
  `FileNotFoundException` at runtime, not just a build warning.
- **Vogen + EF Core:** Vogen can generate an `EfCoreValueConverter` nested type directly on the
  value object, but that requires the value object's own project to reference EF Core — which
  leaks a persistence concern into the Domain layer. This repo keeps Domain persistence-agnostic
  (`Conversions.SystemTextJson` only) and hand-writes `ValueConverter<T,TPrimitive>` classes in
  Infrastructure instead — see [`ValueConverters.cs`](src/MediatrUnionPoc.Infrastructure/ValueConverters.cs).
- **Persistence is SQLite, so transactions are real.** `EfCoreUnitOfWork.BeginTransactionAsync`
  always opens a database transaction (a failure to open one propagates), commit saves then commits
  it, and rollback rolls it back and detaches every tracked entry so a later commit cannot persist
  staged changes. Runtime and tests both use SQLite; with no `ConnectionStrings:Products` value the
  app uses a private in-memory database kept alive by one open connection, with the schema created
  at startup (`EnsureCreated`, no migrations). The class is named for the persistence technology it
  adapts (EF Core), not for a provider: a future NHibernate adapter would be a separate
  `IUnitOfWork` implementation.
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
  `"GetById"` — breaking `CreatedAtAction(nameof(GetByIdAsync), ...)`'s link generation, since
  `nameof` gives the C# identifier, not the trimmed action name MVC would otherwise register it
  under. `Program.cs`'s `AddControllers(...)` call sets `SuppressAsyncSuffixInActionNames = false`
  so action names keep the `Async` suffix this repo's naming convention requires everywhere else.
  [`ProductsControllerTests`](tests/MediatrUnionPoc.Api.IntegrationTests/ProductsControllerTests.cs) exercises
  the real ASP.NET Core host end to end, including following the `Location` header `CreatedAtAction`
  returns, specifically so a link-generation mismatch like this can't pass silently.
- **Compile-time proof, not just documentation:** [`ExhaustivenessTests`](tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs)
  shells out to `dotnet build` against four tiny scratch projects under `tests/CompileTimeChecks/`
  (excluded from `MediatrUnionPoc.slnx` on purpose) to prove a non-exhaustive `switch` over a union
  — and, separately, a non-exhaustive `ITransactionOutcome.ShouldCommit` — is a real compiler error
  (`CS8509`), using the exact installed SDK compiler rather than an in-process Roslyn NuGet
  package — the latter could easily predate this brand-new preview language feature and silently
  fail to reproduce the behavior being tested.

## Glossary

This project sits at the intersection of a few architectural patterns, MediatR's own vocabulary,
one third-party library (Vogen), and a brand-new language feature. This is reference material —
read what you need when a term above is unfamiliar; it isn't required front-to-back reading before
the rest of this document makes sense. Where a term is a general C#/.NET/HTTP concept rather than
something specific to this repo, its entry links to the official documentation.

### Architectural patterns

| Term | Meaning |
| --- | --- |
| **CQRS** (Command Query Responsibility Segregation) | Splits every operation into a **command** (changes state, returns little more than "did it work") or a **query** (reads state, never changes it). Each side can be reasoned about, validated, and optimized independently instead of one method doing both. |
| **Mediator pattern** | Callers don't invoke handlers directly; they send a message to a mediator, which finds the one handler registered for it. A controller sending `CreateProductCommand` has no compile-time dependency on `CreateProductHandler` at all. MediatR is the library implementing this pattern here. |
| **Pipeline (middleware) pattern** | Cross-cutting concerns (logging, validation, transactions) are applied as a chain of wrapping steps every request passes through, instead of being duplicated inside every handler. ASP.NET Core's HTTP middleware pipeline is the same idea one layer up the stack; MediatR's pipeline behaviors are the same idea for in-process messages. |
| **Vertical slice architecture** | Code is organized by *feature* (`Features/Products/Create/` holds that operation's command, handler, validator, and result together) rather than by *technical layer* (a `Commands/` folder, a `Handlers/` folder, each containing pieces of every feature). Changing one operation touches one folder. |
| **Repository pattern** | An interface (`IProductRepository`) hiding how entities are actually fetched or persisted behind method calls that read like domain operations (`GetByIdAsync`, `AddAsync`) — the caller doesn't know or care whether that's EF Core, a REST call, or a file. |
| **Unit of Work pattern** | A single object (`IUnitOfWork`) that tracks everything changed during one logical operation and commits or rolls it all back together, so a handler touching multiple repositories doesn't have to save each one individually and risk a partial write. See [Unit of Work: one session, every repository](#unit-of-work-one-session-every-repository) for the fuller walkthrough. |

### C# language concepts

| Term | Meaning |
| --- | --- |
| **[Generics](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/generics)** | Writing code once against a type parameter (`T`, `TRequest`, `TResponse`) instead of once per concrete type. `IRequestHandler<TRequest, TResponse>` is generic so one interface serves every command and query without duplication. |
| **[Interface](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/interfaces)** | A contract listing members a type must implement, without providing its own implementation. `IProductRepository`, MediatR's `IRequestHandler<,>`, and this repo's own `IValidatable<TSelf>` are all interfaces. |
| **[Static abstract interface member](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-11#static-abstract-members-in-interfaces)** *(C# 11+)* | An interface member marked `static abstract`: every *implementing type* — not every instance — must supply it, and it's callable through a generic type parameter constrained to that interface (`where TSelf : IValidatable<TSelf>`). This is what lets fully generic pipeline code construct or query a concrete union type it has never seen, by calling `TResponse.FromValidationErrors(...)` or `TResponse.ShouldCommit(...)` — impossible with an ordinary instance member, since there's no instance to call it on yet. See [Static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen) above for why this wasn't cleanly possible before C# 11. |
| **[Record](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records)** | A type (`record`, or `record struct`) with compiler-generated structural equality (same property values ⇒ `==`), a generated `ToString()`, and non-destructive mutation via `with`. Used here for immutable shapes like `ProductDto` and every command/query. |
| **Struct vs. class** ([value types](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/value-types) vs. [reference types](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/reference-types)) | A `struct` is a value type — copied by value, usually stack-allocated, not nullable by default. A `class` is a reference type — copied by reference, heap-allocated, nullable by default. A `union` compiles down to a `struct` (see below), which is part of why it carries no boxing tax for the common case. |
| **[Pattern matching / `switch` expression](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/switch-expression)** | `switch` used as an *expression* producing a value (`var x = input switch { ... }`) rather than a statement that just branches. Every case is tested against the switched value's shape or type, and — for a closed set of possibilities like a union — the compiler can prove every case was handled. |
| **Union type** *(C# 15 preview — the concept this whole repo tests; see the [discriminated unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions.md) and [champion issue #9662](https://github.com/dotnet/csharplang/issues/9662), since no stable Learn docs exist yet)* | A closed set of "case types" declared as `union Name(CaseA, CaseB, CaseC)`. A value of that type is always exactly one of the listed cases, never null, never anything outside the list. See [The C# `union` type](#the-c-union-type) for the full mechanics. |
| **Exhaustiveness checking** | The compiler verifying that a `switch` over a closed set of possibilities (a union, or an enum with no `default`) handles every member of that set, refusing to compile (`CS8509`) if one is missing. This is what turns "forgot to handle a new case" into a build failure instead of a runtime gap. |
| **[Source generator](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)** | A compiler plugin that generates additional C# source at build time, usually driven by an attribute. Vogen uses one to turn a six-line `ValueObject<Guid>` declaration into a full value type with factories, equality, and serialization support — see [Vogen: avoiding primitive obsession](#vogen-avoiding-primitive-obsession). |

### MediatR vocabulary

| Term | Meaning |
| --- | --- |
| **`IRequest<TResponse>`** | MediatR's marker interface for "this message expects a `TResponse` back." Every command and query here implements it indirectly through this repo's own `ICommand<TResponse>` / `IQuery<TResponse>` / `ITransactionalCommand<TResponse>` (below). |
| **`IRequestHandler<TRequest, TResponse>`** | MediatR's interface for "the one place that knows how to handle a `TRequest` and produce a `TResponse`." MediatR resolves and calls exactly one registered handler per request type. |
| **`IPipelineBehavior<TRequest, TResponse>`** | MediatR's interface for a pipeline step wrapping every request's handling: it receives the request plus a delegate to call the *next* step (another behavior, or the handler itself). `LoggingBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, and `TransactionBehavior` all implement this, and run in the order they're registered. |
| **`ICommand<TResponse>` / `IQuery<TResponse>` / `ITransactionalCommand<TResponse>`** *(this repo's own marker interfaces — not part of MediatR)* | Sit between `IRequest<TResponse>` and a concrete request to say which pipeline behaviors apply: `IQuery<TResponse>` never runs `TransactionBehavior`; `ICommand<TResponse>` may mutate state with no transaction assumption; `ITransactionalCommand<TResponse>` additionally requires its `TResponse` implement `ITransactionOutcome<TResponse>`. See [`Messages.cs`](src/MediatrUnionPoc.Application/Common/Abstractions/Messages.cs). |
| **`INotification` / `INotificationHandler<TNotification>` / `IPublisher`** | MediatR's *other* messaging shape, distinct from request/response: `IPublisher.Publish(notification)` fans a message out to zero-or-many `INotificationHandler<TNotification>` subscribers, none of which can return a value back or affect one another. Not used by this repo's Products feature today, but the mechanism [Extending the pattern: syncing a search index](#extending-the-pattern-syncing-a-search-index) sketches. |

### Vogen vocabulary

| Term | Meaning |
| --- | --- |
| **Value object** | A type identified by *what it holds*, not by identity, and typically immutable — two `Money` instances wrapping `9.99m` are the same value. Contrast with an *entity* (`Product`), which keeps its identity even as its other properties change. |
| **Primitive obsession** | A code smell where domain concepts (a product ID, a monetary amount) are represented directly by a bare primitive (`Guid`, `decimal`) instead of their own type — letting an arbitrary `Guid` be passed where a `ProductId` was meant, or a negative `decimal` be accepted where a `Money` should never have been constructible at all. Vogen exists to eliminate this at compile time with almost no boilerplate. |
| **`[ValueObject<T>]`** *(Vogen-specific)* | Vogen's attribute; placed on a `partial struct`, it triggers the source generator to build that value type's factories, equality, and (per selected `Conversions`) serialization/EF Core support around the wrapped primitive `T`. |

### This repo's own types

| Term | Meaning |
| --- | --- |
| **`IValidatable<TSelf>`** *(project-specific)* | An interface a union implements (`static abstract TSelf FromValidationErrors(ValidationErrors)`) so `ValidationBehavior` can build that union's own validation-failure case generically, without ever naming the concrete union type. See [Static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen). |
| **`ITransactionOutcome<TSelf>`** *(project-specific)* | An interface a union implements (`static abstract bool ShouldCommit(TSelf)`) so `TransactionBehavior` can ask the union itself whether to commit or roll back, without inspecting which case type came back by name. See [Shared case types are meaning-free](#shared-case-types-are-meaning-free-transactionbehavior-cant-assume-what-a-case-means). |
| **`IAuthorizable<TSelf>`** *(project-specific)* | An interface a union implements (`static abstract TSelf FromNotAuthorized(NotAuthorized)`) so either authorization pattern can build that union's own `NotAuthorized` case generically. See [Authorization](#authorization). |
| **Case type** *(C# spec term)* | Any one of the types listed in a union's declaration (`union Name(CaseA, CaseB, CaseC)` — `CaseA`, `CaseB`, and `CaseC` are all case types of `Name`). This repo further splits case types into two roles it names itself — **shared** and **bespoke**, below — because the spec term alone doesn't distinguish them. |
| **Shared case type** *(project-specific)* | A plain, meaning-free record (`Success`, `NotFound`, `Error`, ...) reused across many unions. A shared case type's identity never implies what it means for commit/rollback or anything else — only the union that declares it decides that; see [Case types used here](#case-types-used-here). |
| **Bespoke case type** *(project-specific)* | A case type carrying one operation's actual payload (`ProductDto`, a hypothetical `PongDto`) rather than a meaning-free shared record. It can still appear in more than one union — `ProductDto` is the success case of both `CreateProductResult` and `GetProductByIdResult`, and again wrapped in `PagedResult<ProductDto>` inside `GetPagedProductsResult` — but unlike a shared case type, that reuse is because those operations happen to succeed with the same payload shape, not because its identity is deliberately meaning-free. |

### Cross-cutting concepts

| Term | Meaning |
| --- | --- |
| **[Dependency Injection (DI)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)** | A pattern where a type declares what it needs via constructor parameters (`IProductRepository repository`) instead of constructing its own dependencies, and a container (ASP.NET Core's built-in one, here) supplies them at runtime based on how they were registered (`AddScoped`, `AddSingleton`, `AddTransient`). This is what lets `IUnitOfWork` and every repository sharing it come from the same request scope automatically — see [Unit of Work: one session, every repository](#unit-of-work-one-session-every-repository). |
| **RFC 7807** ([Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807)) | A standardized JSON shape (`type`, `title`, `status`, `detail`, plus per-field `errors`) for describing an HTTP API error response, instead of every API inventing its own error format. ASP.NET Core's `ProblemDetails`/`ValidationProblemDetails` types, which this repo's controllers return directly, implement it. |
| **LDM (Language Design Meeting)** | The C# language design team's recurring meeting where proposals — like `union` itself — are discussed and decided. Publicly tracked as "champion" issues and meeting notes in the [`dotnet/csharplang`](https://github.com/dotnet/csharplang) repository; [issue #9662](https://github.com/dotnet/csharplang/issues/9662) is the champion/tracking issue for this feature. |
| **Idempotency** | An operation that produces the same end state no matter how many times it's applied. Deleting an already-deleted product and getting `NotFound` both times is idempotent *in effect* — the end state is identical either way — even though `DeleteProductCommand` isn't idempotent *in result*: the first call returns `Success`, a repeat returns `NotFound`, two different case types for the same eventual state. |
| **Domain event** | A record of something meaningful that happened in the domain (`ProductCreated`, `OrderShipped`), raised by the code that caused it and handled by other code reacting to it, decoupled from the operation that raised it. This repo doesn't implement domain events itself, but [Extending the pattern: syncing a search index](#extending-the-pattern-syncing-a-search-index) sketches the closely related idea using MediatR notifications directly. |
| **Eventual consistency** | A guarantee that two copies of data (here, the write-model database and a search index) will agree *eventually*, not immediately — as opposed to *strong*/*immediate* consistency, where every reader sees every write the instant it commits. Acceptable when a brief window of staleness is harmless (a search index); unacceptable when it isn't (an inventory count a checkout flow depends on). |
| **Optimistic concurrency** | Detecting a conflicting concurrent update *after the fact*, typically by comparing a version/timestamp at save time and rejecting the write if it's stale — "assume no conflict, verify before committing." The speculative `Conflict(currentVersion)` case type in [Speculative shared case types](#speculative-shared-case-types-for-a-larger-api) is what an optimistic-concurrency failure would report. |
| **Pessimistic concurrency** | Preventing a conflicting concurrent update *before it can happen*, typically by holding a lock for the duration of an operation — "assume conflict, block others until done." The speculative `Locked(heldBy)` case type is what this would report when it blocks a caller. |

## Footnotes

[^eventual-consistency]: The search index and the write-model database briefly disagree between the
    write committing and the notification handler finishing its Elasticsearch call — typically
    milliseconds, but not zero. See **Eventual consistency** in the
    [Glossary](#cross-cutting-concepts) for the general concept this trades against *immediate*
    consistency.

[^mediatr-license]: MediatR's own license changed starting with v10 — free for individuals and
    small organizations, commercial licensing applies above a revenue threshold. See
    [MediatR's licensing page](https://github.com/jbogard/MediatR/blob/master/LICENSE.md#other-licenses)
    for current terms before adopting it in anything beyond a POC; they've changed before and may
    change again.

# The C# `union` type

Part of the [documentation](index.md).

**Contents**

- [The C# `union` type](#the-c-union-type)
  - [Switch-and-unwrap: why controllers never return the union directly](#switch-and-unwrap-why-controllers-never-return-the-union-directly)
  - [Overriding one arm, or writing your own extension member](#overriding-one-arm-or-writing-your-own-extension-member)
- [Static abstract interface members: why generic code can build a union it's never seen](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)

> [!TIP]
> Deeper, citation-backed research behind this section lives in
> [`docs/research/csharp-union-type-research.md`](research/csharp-union-type-research.md) —
> primary sources (language reference, feature spec, [LDM](glossary.md#cross-cutting-concepts) issue, compiler
> bug tracker) for every claim below, plus open questions not yet settled upstream.

```csharp
public union CreateProductResult(ProductDto, ValidationErrors, Error, Conflict);
```

This declares a closed set of four **[case types](glossary.md#this-repos-own-types)**. The compiler generates
a [struct](glossary.md#c-language-concepts) implementing `IUnion { object? Value { get; } }`, plus an implicit
conversion from each case type:

```csharp
CreateProductResult ok = ProductDto.FromDomain(product);        // implicit conversion
CreateProductResult bad = new Error("boom", "BOOM");            // implicit conversion
```

[Pattern matching](glossary.md#c-language-concepts) unwraps to the *contained* case, not the union wrapper
itself:

```csharp
var response = result switch
{
    ProductDto dto => Ok(dto),          // matches result.Value is ProductDto
    ValidationErrors e => BadRequest(e),
    Error err => Problem(err.Message),  // or err.ToProblemResult(HttpContext), see below
    Conflict c => Conflict(c.Message),
    // no default/discard needed — the compiler knows these are the only four cases
};
```

Unions can carry a body, including implementing [interfaces](glossary.md#c-language-concepts) — which is how
this repo gets a union to expose a static factory method usable from fully generic code (see
[Static abstract interface members](#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)
below). This only works because C# allows **static abstract members on interfaces** —
without that, a generic pipeline behavior would have no way to construct an arbitrary union type
it has never seen.

## Switch-and-unwrap: why controllers never return the union directly

Every action in [`ProductsController`](../src/MediatrUnionPoc.Api/Controllers/ProductsController.cs)
`switch`es on the union and returns a plain DTO/RFC 7807 problem/status code — it never does
`return Ok(result)` with the raw union itself. The `switch` stays in the controller so the
compiler keeps enforcing exhaustiveness (`CS8509`); only the repeated failure *arms* are one-liners
that call C# 14 extension members from [`Api/Http`](../src/MediatrUnionPoc.Api/Http/):

```csharp
return result switch
{
    ProductDto dto => Ok(dto),
    NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product"),
    Error error => error.ToProblemResult(HttpContext),
};
```

Each `ToProblemResult` (on `Error`, `NotFound<TId>`, `NotAuthorized`, `ValidationErrors`,
`PreconditionFailed`, `Conflict` and the Api-level `MissingIfMatch`) builds an
`application/problem+json` body, so every non-2xx response in the API is RFC 7807 (the not-found
body carries a `code` member, `"NOT_FOUND"`). Shared policy lives in `HttpMappingOptions`,
registered in `Program.cs` with `AddResultHttpMapping(...)`: an `Error.Code` to HTTP status table
(default: `Error.ValidationFailureCode` is 400, every other code 500) and a switch for RFC 7807
`type` URIs. Each extension also takes optional per-call overrides (`statusCode`, `title`,
`detail`), and nothing is sealed: a controller can write any arm by hand, or its own extension
members over the same case types; see
[Overriding one arm, or writing your own extension member](#overriding-one-arm-or-writing-your-own-extension-member).

> [!NOTE]
> It's not because `System.Text.Json` would otherwise serialize the generated struct's own
> `Value` property as a wrapper. .NET 11's `System.Text.Json` has built-in awareness of `IUnion`
> and transparently flattens to whichever case type is boxed inside, with no wrapper at all:
> `JsonSerializer.Serialize((CreateProductResult)new ProductDto(...))` produces the exact same
> bytes as serializing the `ProductDto` directly. See
> [`UnionJsonSerializationTests`](../tests/MediatrUnionPoc.Application.Tests/Unions/UnionJsonSerializationTests.cs)
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

## Overriding one arm, or writing your own extension member

The mapping has three layers, and each can be changed without touching the others. All snippets
below are taken from the repo (`ResultHttpMappingTests`, `ResultHttpExtensions`, `ProductsController`).

**1. Change shared policy**, once, in `Program.cs` or a test host. Here a custom error code gets its
own status, and the built-in validation code is remapped:

```csharp
services.AddResultHttpMapping(o => o.ErrorStatusCodes["OUT_OF_STOCK"] = 503);

services.AddResultHttpMapping(options =>
    options.ErrorStatusCodes[Error.ValidationFailureCode] = StatusCodes.Status422UnprocessableEntity);
```

`HttpMappingOptions` also holds `DefaultErrorStatusCode` (500), `IncludeTypeUris` and the status to
`type` URI table `TypeUris`. It is an ordinary options class, so `services.Configure<HttpMappingOptions>(...)`
works as well.

**2. Override a single arm.** Every extension member takes optional `statusCode`, `title` and `detail`
parameters, so one controller arm can differ from the rest without a new type:

```csharp
NotFoundCase notFound => notFound.ToProblemResult(HttpContext, resource: "Product", statusCode: 410),
```

An arm is just an expression of type `IActionResult`, so it can equally be hand-written
(`Ok(...)`, `Problem(...)`, `StatusCode(...)`) instead of calling an extension at all; the
`switch` around it still has to cover every case.

**3. Define your own extension member.** The extensions are C# 14 `extension` blocks, so a member
over a case type (or over any other type, such as `ClaimsPrincipal`) is a
new `extension` block in any static class. A property over the caller's principal, say, needs
nothing but public types:

```csharp
extension(ClaimsPrincipal principal)
{
    public string? CallerId => principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
```

and this is the shape of a per-case member, the built-in `Conflict` one (its XML docs and null
guards trimmed). `BuildProblem` and `OptionsOf` are private helpers of `ResultHttpExtensions`; an
extension of your own would create its `ProblemDetails` the same way through the registered
`ProblemDetailsFactory`:

```csharp
extension(Conflict conflict)
{
    public IActionResult ToProblemResult(
        HttpContext http,
        int? statusCode = null,
        string? title = null,
        string? detail = null
    )
    {
        return BuildProblem(
            http,
            statusCode ?? StatusCodes.Status409Conflict,
            title ?? "Conflict",
            detail ?? conflict.Message
        );
    }
}
```

Because the controller keeps its own `switch`, none of this weakens exhaustiveness: adding a case to
a union still fails the build until an arm exists for it.

# Static abstract interface members: why generic code can build a union it's never seen

[`IValidatable<TSelf>`](../src/MediatrUnionPoc.Application/Common/Abstractions/IValidatable.cs),
[`ITransactionOutcome<TSelf>`](../src/MediatrUnionPoc.Application/Common/Abstractions/ITransactionOutcome.cs),
and [`IAuthorizable<TSelf>`](../src/MediatrUnionPoc.Application/Common/Abstractions/IAuthorizable.cs)
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
instance — must supply it, and it becomes callable through a [generic](glossary.md#c-language-concepts) type
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

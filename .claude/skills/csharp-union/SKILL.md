---
name: csharp-union
description: Best practices for C#'s `union` type, a C# 15 / .NET 11 preview language feature for closed, compiler-exhaustive sets of type alternatives (result-or-error returns, message dispatch, replacing marker interfaces). Use when declaring, consuming, or reviewing `union` declarations, switching/pattern-matching over a union's cases, hitting CS8509/CS8655, or designing MediatR/CQRS-style response types with unions.
---

# C# union types

Preview-only (C# 15 / .NET 11 preview, needs `<LangVersion>preview</LangVersion>` and a preview
SDK per `global.json`). Details below may still change before final release — verify
surprising behavior against the installed SDK rather than assuming these notes are eternal.
Full citations and open questions: [REFERENCE.md](REFERENCE.md).

## Quick start

```csharp
public union Pet(Cat, Dog, Bird);   // shorthand declaration — a plain struct under the hood

Pet pet = new Dog("Rex");           // case type converts to the union implicitly, no wrapping call

var name = pet switch
{
    Dog d => d.Name,   // patterns match against pet.Value, not pet itself
    Cat c => c.Name,
    Bird b => b.Name,
    // no discard needed — compiler knows this is exhaustive
};
```

That implicit case-type → union conversion is why a handler can just `return new ProductDto(...);`
against a `union CreateProductResult(ProductDto, ValidationErrors, Error)` return type — no
explicit `new CreateProductResult(...)` needed.

## When to reach for a union

Microsoft's own stated guidance (`Declare a union when...`, C# language reference): a value must
be exactly one of a fixed set of types and you want the compiler to enforce every case is
handled. Named scenarios: **result-or-error returns** (`union Result(Success, Error)`),
**message/command dispatch** over a closed set of types, and **replacing marker
interfaces/abstract base classes** used only to group types for pattern matching.

Contrast: a union isn't a `class`/`struct` (adds no new data members — composes existing
types), isn't an `interface` (it's closed, not extensible), and isn't a `record` (adds no
equality/cloning/deconstruction — it's a plain `struct`, never `record struct`).

## Core rules

- **Exhaustiveness is CS8509, and it's a warning by default**, not an error. To make a
  non-exhaustive switch actually fail the build, the consuming project needs
  `WarningsAsErrors` (or repo-wide `TreatWarningsAsErrors`) covering it — don't tell anyone
  exhaustiveness is "enforced at compile time" without that caveat.
- A **second, separate diagnostic (CS8655)** fires when a "maybe null" union's `Value` isn't
  handled by a `null` arm, even if every case type is covered. Case-type exhaustiveness and
  null-exhaustiveness are independent checks.
- Only `switch` **expressions** are documented as exhaustive-checked. `switch` statements
  follow ordinary (non-exhaustive) C# switch-statement rules — this is inferred from every
  primary-source example using expression syntax, not stated outright, so verify empirically
  if load-bearing.
- **Patterns unwrap to `.Value` by default** — a type pattern (`Cat c`), property/positional
  pattern with a type, constant pattern, and relational pattern all match against the union's
  boxed `Value`. **Three exceptions match the union itself, not `.Value`: `var`, discard `_`,
  and `not`.** `not` is the one people trip on most — see the full per-pattern table in
  [REFERENCE.md](REFERENCE.md#pattern-matching-unwrap-rules).
- `u is SomeUnionType` is invalid — you match against **case types**, never the union type's
  own name, in a pattern.

## Gotchas worth flagging in review

- **Boxing.** The shorthand `union(...)` form always stores `Value` as a single `object?`
  field, so every value-type case (`int`, etc.) is boxed on entry. There's no opt-in flag for
  unboxed storage on the shorthand form — the only escape hatch is hand-writing a
  non-boxing union with `HasValue`/`TryGetValue(out T)` members per case type (each member is
  independently optional; the compiler falls back to `Value`-based checks for whichever one is
  missing). See the worked example in [REFERENCE.md](REFERENCE.md#non-boxing-pattern).
- **Null semantics differ by union kind** — struct union, class union, and nullable-wrapped
  struct union (`Pet?`) each resolve a bare `null` pattern differently. Class unions are the
  confusing case: `result is null` compiles to `result == null || result.Value == null`, and
  Microsoft's own spec calls this design point "clearly optimized around the expectation that a
  union type is a struct." Full table: [REFERENCE.md](REFERENCE.md#null-semantics).
- **Deriving from a class-based union is dangerous** — a derived class inherits `IUnion` and
  becomes its own union with case types drawn from its own constructors, which can silently
  invalidate exhaustiveness checks written against the base type. Avoid inheritance from union
  classes.
- **`System.Text.Json` support is not yet documented or confirmed shipped.** Don't assert
  concrete wire-format behavior (e.g. "unions serialize with no `$type` field") as settled fact
  without checking the installed preview SDK — the design exists only as GitHub API proposals,
  one of which is explicitly AI-drafted per its own disclaimer.
- **No Microsoft-published case-type naming convention exists.** Case types are just ordinary
  types (classes, records, structs, primitives, interfaces, generics, other unions) — name them
  by your own project's conventions.
- Union-of-unions nesting and generic case types are both spec-supported, but nesting has no
  worked example in any primary source — treat as documented-but-thin.
- **No synthesized equality or `ToString`** — a union is a plain `struct`, never `record struct`,
  so `ToString()` prints the union's own type name (e.g. `"MyApp.Pet"`), not `Value`'s, and `==`
  falls back to default struct equality over the single boxed `Value` field (works out when the
  case type overrides `Equals`, e.g. records — not otherwise). Inferred from the documented
  lowering in [REFERENCE.md](REFERENCE.md#declaration-syntax-and-lowering), not stated outright
  by any primary source — verify empirically if load-bearing.

## Using unions as MediatR/CQRS response types

This maps directly onto Microsoft's "result-or-error returns" and "replacing marker
interfaces" scenarios above: a handler returns `union CreateProductResult(ProductDto,
ValidationErrors, Error)` instead of throwing or returning a nullable/marker-interface type,
and the caller's `switch` is exhaustive over every outcome the operation can produce. Keep
case types meaning-free and reused across unions when case *identity* (e.g. commit vs.
rollback) needs to differ per-union — decide that via a `static abstract` member on the union
itself (e.g. `ShouldCommit(TResponse)`), not by trying to infer meaning from a shared case
type. See [REFERENCE.md](REFERENCE.md#interop) for the `IUnion`/`IUnionMembers` mechanics this
relies on.

## Reference

[REFERENCE.md](REFERENCE.md) — full pattern-matching table, null-semantics table, generics,
nesting, STJ proposal details, comparison to F#/Rust/TypeScript unions (marked as synthesis,
not Microsoft-sourced), and the open-questions list (preview-number conflicts, unverified
claims, thread-safety).

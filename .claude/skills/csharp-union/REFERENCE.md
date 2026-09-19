# C# union types — reference

Detailed backing material for [SKILL.md](SKILL.md). Sourced from official Microsoft docs and
the csharplang spec as of 2026-09-18; this is a preview feature, so treat anything here as
subject to change and re-verify against the installed SDK before relying on it for something
load-bearing. Primary sources:

- [Union types — C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union) ("LR")
- [Unions — C# feature specifications (preview)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-15.0/unions) ("Spec")
- [dotnet/csharplang proposals/unions.md](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md) (canonical spec; has the "Open questions" log)
- [Explore union types in C# 15 — .NET Blog](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/)

## Declaration syntax and lowering

Shorthand form:

```csharp
public union Pet(Cat, Dog, Bird);
```

Grammar (Spec): a union declaration is syntactically a `struct` declaration with `struct`
swapped for `union` and a parenthesized case-type list inserted — it can carry `partial`,
struct modifiers, attributes, type parameters, constraint clauses, and an interface list.

A body is optional and, when present, may add members but not instance fields, auto-properties,
or field-like events; an explicitly declared constructor must delegate via `this(...)` to a
generated constructor, and its single parameter must be by-value or `in` (never `ref`/`out`):

```csharp
public union Length(Meters, Feet)
{
    public double TotalMeters => this switch
    {
        Meters m => m.Value,
        Feet f => f.Value * 0.3048,
        _ => throw new InvalidOperationException("The Length has no value."),
    };

    public Length Add(Length other) => new Meters(TotalMeters + other.TotalMeters);
}
```

**Resolved design decision**: a union is a plain `struct`, never a `record struct` — an earlier
spec draft had it as `record struct` and this was explicitly reversed. `union Pet(Cat, Dog,
Bird);` lowers to:

```csharp
[Union] public struct Pet : IUnion
{
    public Pet(Cat value) => Value = value;
    public Pet(Dog value) => Value = value;
    public Pet(Bird value) => Value = value;
    public object? Value { get; }
}
```

Always a struct, always a single `object?` field, always boxes value-type cases on entry.

## What can be a case type

"Any type that converts to `object`" — classes, records, structs, interfaces, type parameters,
nullable types, primitives, and other unions (LR, Spec). Overlapping cases are explicitly fine.

```csharp
public union Pet(Cat, Dog, Bird);              // classes/records
public union Option<T>(None, Some<T>);         // generic case type
public union IntOrString(int, string);         // primitives — boxed when stored
```

Nullable value-type case types are supported, with one wrinkle: the generated
`TryGetValue`'s out-parameter uses the *underlying* non-nullable type, because a type pattern
can't target a nullable value type directly.

## Exhaustiveness rules

Core rule (LR/Spec): a `switch` **expression** is exhaustive when it handles every case type of
a union — no `_`/`var` catch-all required.

```csharp
var name = pet switch
{
    Dog d => d.Name,
    Cat c => c.Name,
    Bird b => b.Name,
    // No discard needed, no warning
};
```

**CS8509** fires when a case type is missing. It is a **warning by default**, not an error.
Escalate deliberately if you want it to break the build:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsAsErrors>CS8509</WarningsAsErrors>
</PropertyGroup>
```

(A real compiler bug, [dotnet/roslyn#83666](https://github.com/dotnet/roslyn/issues/83666),
used to misfire CS8509 on exhaustive single-case unions; fixed/closed — if you see this on an
old preview build, upgrade.)

**CS8655** is the separate null-exhaustiveness diagnostic. Even with every case type covered,
a union whose `Value` null-state is "maybe null" still warns without a `null` arm:

```csharp
Pet pet = GetNullableDog(); // pet.Value is "maybe null"
var value = pet switch
{
    Dog dog => ...,
    Cat cat => ...,
    // warning CS8655: the pattern 'null' is not covered
};
```

Neither Learn page names CS8655 in prose — only the Spec's literal compiler-output quote
surfaces it, so it's easy to miss in casual reading.

Switch **statements** (`switch (x) { case ... }`) are never shown as exhaustive-checked in any
primary-source example — treat them as following ordinary (non-exhaustive) C# switch-statement
rules; this is inferred, not explicitly documented either way.

## Pattern matching unwrap rules

Central rule: patterns generally apply to the union's `Value` property, not the union value
itself — described as the union being "transparent" to pattern matching.

```csharp
var name = pet switch
{
    Dog d => d.Name,   // 'Dog d' is really matched against pet.Value
    Cat c => c.Name,
    Bird b => b.Name,
};
```

| Pattern kind | Unwraps to `.Value`? | Notes |
|---|---|---|
| `var` | No | Captures the union itself. |
| Type pattern (`Cat c`) | Yes | `p is Pet` is an **error** — match case types, never the union's own type name. |
| Declaration pattern | Yes | Equivalent to `type and var designation`. |
| Property pattern with a type (`Cat { Name: "Fido" }`) | Yes | Without a leading type, matches the union instance's own shape only. |
| Positional pattern with a type | Yes | Same logic as property pattern. |
| Constant pattern (`is 10`) | Yes | Succeeds only when the union instance is non-null *and* `Value` matches — two implicit null checks for class unions. |
| Discard `_` | No | |
| Relational pattern (`is > 1`) | Yes | |
| List pattern | No | Doesn't unwrap by default; retrofit via C# 14 extension blocks adding `Length`/indexer to `object` if needed — obscure, not automatic. |
| `not` | No | Applies to the incoming union value, not `.Value`. Most likely pattern kind to trip people up. |
| Logical (`and`/`or`) | Depends on sub-pattern | Each branch evaluated independently; `and` can change what the right-hand pattern sees, `or` cannot. |

```csharp
GetPet() switch
{
    var pet and not null => ...,   // 'var pet' captures Pet?; 'not null' still checks Pet?, not .Value
    not null and var value => ..., // order matters — same reasoning, opposite order
}
```

`u is int` on a union reads like a runtime type-check but is resolved to **behave as a type
pattern** (i.e. it does unwrap) — this was a deliberate, debated resolution to avoid a
confusing alternative.

## Generics

Both the union type and its case types can be generic:

```csharp
public record class None;
public record class Some<T>(T Value);
public union Option<T>(None, Some<T>);
```

```csharp
public union OneOrMore<T>(T, IEnumerable<T>)
{
    public IEnumerable<T> AsEnumerable() => Value switch
    {
        T single => [single],
        IEnumerable<T> multiple => multiple,
        null => []
    };
}
```

**A type parameter is never itself treated as a union, even when constrained to one**:

```csharp
static bool Test1<T>(T u) where T : C1  // C1 is a union
{
    return u is int; // ordinary is-type check against T, NOT union unwrapping
}
```

Don't write generic helpers over `where T : SomeUnion` expecting pattern-unwrap behavior.

## Nesting

Explicitly supported ("It is fine for resulting cases to overlap, and for unions to nest or be
null" — Spec) but **no worked code example exists in any primary source**. Treat as
documented-but-thin; verify empirically before depending on specific nested-union behavior.

## Non-boxing pattern

The shorthand `union(...)` form always boxes value-type cases. To avoid boxing, hand-write
`HasValue`/`TryGetValue(out T value)` members per case type — each is independently optional,
and the compiler falls back to `Value`-based checks for whichever is missing.

## Interop

**`IUnion` / `UnionAttribute`** (shipped in the BCL — LR states "beginning with .NET 11 Preview
5"; secondary sources gave conflicting preview numbers, so verify against your installed SDK):

```csharp
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class UnionAttribute : Attribute;

public interface IUnion
{
    object? Value { get; }
}
```

```csharp
if (value is IUnion { Value: null }) { /* generic null check across any union type */ }
```

**`IUnionMembers`** lets a type delegate its creation/access members to a nested interface
instead of declaring them directly — useful for `record class` unions needing custom
construction, and conceptually related to C# 11 static-abstract-interface-members:

```csharp
public record class Result<T> : Result<T>.IUnionMembers
{
    object? _value;

    public interface IUnionMembers
    {
        static Result<T> Create(T value) => new() { _value = value };
        static Result<T> Create(Exception value) => new() { _value = value };
        object? Value { get; }
    }

    object? IUnionMembers.Value => _value;
}
```

## Null semantics

Three distinct behaviors depending on the union's underlying kind:

| Union kind | `null` pattern succeeds when |
|---|---|
| Struct union | `.Value` is null (`default(SomeUnion)` has a null `Value`) |
| Class union | the reference itself is null **or** `.Value` is null (`result is null` ≡ `result == null \|\| result.Value == null`) |
| Nullable-wrapped struct union (`Pet?`) | the nullable wrapper has no value **or** the underlying union's `.Value` is null |

Class-union null-checking is an explicitly debated, only-partially-satisfying design point — the
Spec's own words: "This part of the design is clearly optimized around the expectation that a
union type is a struct." If a case type in a class union can independently be `null`, the
exhaustiveness example arm is literally `null => 3`, which the docs themselves call "very
confusing."

Nullability tracking: `Value`'s default null state is "maybe null" if any case type's default
null state is "maybe null," otherwise "not null." Creating a union from a case value propagates
that value's null state. `HasValue`/`TryGetValue` narrow `Value`'s null state to "not null" on
the true branch, same as checking `Value` directly.

**Deriving from a class-based union is dangerous**: a derived class automatically inherits
`IUnion` and becomes its own union whose case types come from its own constructors, which can
silently break exhaustiveness checks written against the base type. No guardrail was adopted in
the spec — avoid the pattern rather than relying on the compiler to catch misuse.

## Serialization (System.Text.Json)

**Not documented as shipped** as of the LR page's date (2026-08-14) or the .NET Blog post —
neither mentions STJ behavior. Design work exists only as GitHub proposals:

- [dotnet/runtime#125449](https://github.com/dotnet/runtime/issues/125449) — umbrella issue;
  its own body carries a disclaimer that it was AI-drafted.
- [dotnet/runtime#127299](https://github.com/dotnet/runtime/issues/127299) — union-specific,
  closed/api-approved (not independently confirmed live in a specific preview build). Key
  design points quoted from its body:
  - **No discriminator/envelope on the wire** — unions serialize transparently using the
    case value's own JSON contract, no `$type` field:
    ```csharp
    union Result(int, string);
    JsonSerializer.Serialize<Result>(new Result(42));      // 42
    JsonSerializer.Serialize<Result>(new Result("hello")); // "hello"
    ```
    Rationale given: "Unions don't have a natural discriminator: any case can be picked by the
    union's constructors, and two distinct case constructors can produce equal values."
  - **Deserialization uses first-token dispatch** — the converter inspects the first JSON
    token's kind and picks the unique compatible case type; ambiguous unions (two case types
    that both serialize as JSON objects, say) need an additional "case classifier
    abstraction" described in the same issue.

Don't present concrete STJ union wire behavior as settled fact without re-verifying against the
installed preview SDK.

## Other known limitations

- **Union conversions aren't "real" implicit conversions** — they don't chain with
  user-defined implicit conversions or other union conversions, and an existing user-defined
  `implicit`/`explicit` operator on the source type always takes priority over the union
  conversion.
- **No lifted (`Nullable<T>` → union) conversions** — deliberate, because it would be ambiguous
  whether lifting should produce a union with null `Value` or a null `Nullable<Union>`.
- **Thread-safety is an explicitly open question** — the Spec's closing section leaves
  unresolved whether the compiler should defensively copy `this` to guard against a union
  struct being reassigned mid-method by another thread. Don't assume any concurrency guarantee.
- **Analyzer/tooling gaps are real but not always upstream-documented.** For example, one
  project's `.editorconfig` disabling SA1649 with "crashes on any file containing a `union`
  declaration" is a first-hand finding, not corroborated by a public StyleCopAnalyzers issue at
  research time — treat claims like this as project-local observations unless you can find the
  upstream tracking issue yourself.

## Comparison to other languages' unions

**This section is synthesis, not sourced from Microsoft** — none of the primary docs draw these
comparisons explicitly. Verify independently before treating as authoritative.

- **F# discriminated unions / Rust `enum`** are *tagged* unions with a runtime discriminant the
  compiler bakes in; cases usually aren't standalone types. C# unions are explicitly **not**
  tagged — the Spec says so directly: "The proposed unions in C# are unions of *types* and not
  'discriminated' or 'tagged.'" DU-like ergonomics come from *you* declaring fresh types per
  case, not from the union mechanism synthesizing tags.
- **TypeScript `A | B`** is closer in spirit (union of types, not tags), but C#'s union is a
  **nominal, declared type** you can pass around by name, whereas TypeScript unions are
  structural with no identity beyond the `|` combinator at the use site. A rejected "pure
  union" (`A|B|C` combinator) design alternative, closer to TypeScript, is referenced in the
  Spec's Motivation section but not fetched in full for this research pass.
- **Rust's exhaustiveness is a hard, unconditional compiler error**; C#'s CS8509 is a
  **warning by default** requiring explicit project-level escalation — a real difference in
  default strictness, not just syntax.
- **Runtime representation**: F#/Rust typically store cases inline/unboxed via an optimized
  tagged representation; C#'s shorthand union always boxes value-type cases into a single
  `object?` field unless you hand-write the non-boxing pattern. A real, not cosmetic,
  performance divergence.

## Open questions (documentation thin or unverified)

Index of what's flagged unverified above — check the linked section before relying on any of it:
exact BCL preview number for `IUnion`/`UnionAttribute` ([Interop](#interop)); STJ wire behavior
([Serialization](#serialization-systemtextjson)); no published case-type naming convention;
switch-*statement* exhaustiveness ([Exhaustiveness rules](#exhaustiveness-rules)); the
SA1649-crashes claim ([Other known limitations](#other-known-limitations)); union-of-unions
nesting, no worked example anywhere ([Nesting](#nesting)); thread-safety of struct field access,
explicitly unresolved per the Spec ([Other known limitations](#other-known-limitations)).

One item with no home above: the rejected "pure union" (`A|B|C`) design alternative has its own
overview doc (`union-proposals-overview.md` in csharplang's `meetings/working-groups` folder),
not yet fetched in full — the next read if deeper "why not TypeScript-style" material is needed.

Everything in this file is preview-only and could change before final release, including
exhaustiveness diagnostics, class-union null-checking semantics, and the STJ wire format.

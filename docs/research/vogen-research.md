# Vogen — research notes

Compiled 2026-09-18 as source material for engineers working on this repo's value objects
(`Domain/ProductId.cs`, `Domain/Money.cs`) and their EF Core/JSON integration. Every claim is cited
inline to its primary source. Secondary sources (blog posts, aggregator search summaries) are
explicitly flagged as such — don't treat them as authoritative over the primary docs/repo/source
below. This document follows the same citation discipline as
`docs/research/csharp-union-type-research.md` — read that one first if you haven't; the conventions
(inline citation tags, flagged secondary sources, an "Open questions" close) are identical here.

This repo pins **Vogen 8.0.7** in `Directory.Packages.props`. For how Vogen value objects interact
with FluentValidation and MediatR in this repo's pipeline, see [`mediatr-research.md`](mediatr-research.md)
§3 for the worked sequence diagram, and §3 below for the defense-in-depth pattern this repo uses.

**Primary sources used:**

- [SteveDunn/Vogen GitHub repo](https://github.com/SteveDunn/Vogen) — README ("VG-Readme" below)
- [stevedunn.github.io/Vogen](https://stevedunn.github.io/Vogen/) — official docs site, specifically
  `overview.html`, `faq.html`, `efcoreintegrationhowto.html`, and `integration.html` ("VG-Overview",
  "VG-FAQ", "VG-EfCore", "VG-Integration" below)
- This repo's own source: `Domain/{Money,ProductId}.cs`, `Infrastructure/ValueConverters.cs` ("Repo"
  below)
- [nhibernate/nhibernate-core GitHub repo](https://github.com/nhibernate/nhibernate-core) —
  `src/NHibernate/UserTypes/IUserType.cs` source and its XML doc comments ("NH-IUserType" below)
- [nhibernate.info](https://nhibernate.info) — official NHibernate docs site, specifically
  `doc/nhibernate-reference/mapping.html` ("NH-Mapping" below)
- Direct queries against GitHub's Issues Search API (`repo:SteveDunn/Vogen NHibernate`) and NuGet's
  Search API (`Vogen NHibernate`), run during this research pass rather than summarized from a
  third party ("GH-IssueSearch", "NuGet-Search" below)
- [FluentNHibernate/fluent-nhibernate GitHub repo](https://github.com/FluentNHibernate/fluent-nhibernate) —
  `src/FluentNHibernate/Conventions/{IConvention,IPropertyConvention,IPropertyConventionAcceptance}.cs`,
  `Conventions/Instances/IPropertyInstance.cs`, `Conventions/Inspections/IPropertyInspector.cs`, and
  `MappingModel/TypeReference.cs`, fetched directly from `raw.githubusercontent.com` during this
  research pass ("FNH-Conventions" below)

**Secondary sources (flagged inline wherever used):** WebSearch summaries of Vogen's own
`working-with-ids.html`, `efcoreintegrationhowto.html`, and `casting.html` doc pages, used where a
page wasn't independently re-fetched and quoted verbatim during this research pass — each instance
is flagged individually where used, and listed again in Open Questions.

---

## 1. `[ValueObject<T>]` and generated members

Vogen is "a semi-opinionated ... source generator and code analyser" that wraps a primitive (e.g.
`int`, `Guid`, `decimal`) in a strongly-typed domain value object to combat primitive obsession
(VG-Readme). Decorating a `partial struct` (or `partial class`) with `[ValueObject<T>]` generates,
per VG-Overview:

- A `Value` property exposing the wrapped primitive.
- A static `From(T)` factory — described as the *only* sanctioned way to construct an instance:
  "there is one, and only one, way of creating instances, and that is with the generated `From`
  method" (VG-Overview, quoted). `new`, `default`, and reflection-based construction are all made
  into compile errors by Vogen's companion analyzer (VG-Overview; VG-FAQ confirms `default(CustomerId)`
  on a struct VO "generates a compilation error").
- A `TryFrom` factory (two overloads) as the non-throwing alternative to `From`, for call sites that
  want to handle invalid input without exceptions — returning a `ValueObjectOrError` result type
  shipped in `Vogen.SharedTypes.dll` (found via WebSearch summary of Vogen's own
  `working-with-ids.html`/overview docs; **flagged as not independently re-fetched and quoted
  verbatim in this pass** — see Open Questions). This repo's `ProductId`/`Money` don't use `TryFrom`
  anywhere in the code read for this research (Repo) — they only call `From` (inside a `Validate`
  hook that throws on invalid input) and a custom `ProductId.New()` convenience factory (Repo,
  `Domain/ProductId.cs`).
- Equality (`IEquatable<T>`), ordering (`IComparable<T>`), `GetHashCode()`, and `ToString()`
  overrides — structural, not reference-based (VG-Overview).

## 2. The `Validate` hook and normalization

A private static `Validate(T input) : Validation` method, if declared, is invoked from every
generated factory (`From`, `TryFrom`) and returns `Validation.Ok` or `Validation.Invalid("message")`;
a `From` call with invalid input throws `ValueObjectValidationException` (VG-FAQ, VG-Overview). This
repo uses this exact hook in both its value objects (Repo):

```csharp
// Money.cs
private static Validation Validate(decimal input) =>
    input >= 0 ? Validation.Ok : Validation.Invalid("Money cannot be negative.");

// ProductId.cs
private static Validation Validate(Guid input) =>
    input != Guid.Empty ? Validation.Ok : Validation.Invalid("ProductId cannot be an empty guid.");
```

A `NormalizeInput` hook (`private static T NormalizeInput(T input)`) runs before `Validate` and can
sanitize input — e.g. trimming a string — before validation sees it (VG-FAQ). Neither `Money` nor
`ProductId` in this repo declares a `NormalizeInput` (Repo) — there's nothing to normalize for a
`decimal` or `Guid`; this hook matters more for `string`-backed value objects, which this repo
doesn't currently have.

## 3. Defense-in-depth alongside FluentValidation

This repo's `CreateProductCommand(string Name, decimal Price)` carries primitive-typed properties,
not `ProductId`/`Money` directly — those value objects are constructed only **after**
`ValidationBehavior`'s FluentValidation checks pass, inside the handler (inferred from
`CreateProductCommand`'s primitive-typed parameters, Repo). This means **two independent validation
layers exist for the same business rule** in this repo's `Money`/price case: FluentValidation's
`RuleFor(x => x.Price).GreaterThanOrEqualTo(0)` catches a negative price *before* the transaction
begins and reports it as a structured `ValidationErrors` case (see
[`fluentvalidation-research.md`](fluentvalidation-research.md) §1), while Vogen's `Money.Validate`
would *also* reject a negative value if it were ever constructed from an unvalidated source —
described candidly in `Money.cs`'s own doc comment as deliberate defense in depth: "so a negative
price can never be constructed anywhere in the codebase, not just at the API boundary where
FluentValidation already checks it" (Repo). **Neither MediatR, FluentValidation, nor Vogen's own
docs describe this two-layer pattern** — it's this repo's own design decision, not something drawn
from any primary source's recommended usage.

## 4. EF Core integration — and why this repo hand-writes its own converters

Vogen offers **two** built-in ways to get an EF Core `ValueConverter` for a value object, per
VG-EfCore:

1. **Inline generation on the VO itself**: add `Conversions.EfCoreValueConverter` to the
   `[ValueObject<T>]` attribute (e.g. `[ValueObject<string>(conversions: Conversions.EfCoreValueConverter)]`),
   which generates a nested `EfCoreValueConverter` class directly on the value object type, wired up
   in `OnModelCreating` via `b.Property(e => e.Name).HasConversion(new Name.EfCoreValueConverter())`
   (VG-EfCore).
2. **A separate marker-class approach (.NET 8+)**, using `[EfCoreConverter<T>]` attributes on a
   dedicated partial class that can live in a **different project** from the value objects
   themselves — quoted example from search-located documentation content (VG-EfCore via
   corroborating WebSearch summary of the same official page; **flagged as not independently
   re-fetched verbatim** — see Open Questions):
   ```csharp
   [EfCoreConverter<Domain.CustomerId>]
   [EfCoreConverter<Domain.CustomerName>]
   internal partial class VogenEfCoreConverters;
   ```
   This is explicitly positioned as the answer to exactly the architectural concern this repo has:
   "The key benefit of this approach is that it allows you to create these types in a separate
   project, which is useful if you're using something like Onion Architecture, where you don't want
   your domain objects to reference infrastructure code." A generated `HasVogenConversion()`
   extension method is then available on the DbContext side (VG-EfCore).

> [!NOTE]
> **This repo's own stated rationale, cross-checked against what Vogen actually offers.** This
> repo's `ValueConverters.cs` hand-writes `ProductIdValueConverter`/`MoneyValueConverter` instead of
> using either Vogen option, with this comment: "Vogen can generate an equivalent
> `EfCoreValueConverter` nested type directly on the value object via
> `Conversions.EfCoreValueConverter`, but that would require `MediatrUnionPoc.Domain` to reference EF
> Core just to compile the generated code — leaking a persistence concern into a layer that should
> have none" (Repo, `Infrastructure/ValueConverters.cs`). That rationale is accurate **for option 1**
> (the inline `Conversions.EfCoreValueConverter` flag, which does require the *Domain*-layer assembly
> containing the `[ValueObject<T>]` declaration to compile against EF Core types). **It does not
> address option 2** — the `[EfCoreConverter<T>]` marker-class approach is specifically designed to
> let the converter-generation live in the *Infrastructure* project instead, with zero EF Core
> reference needed in Domain, which is exactly this repo's stated goal. Whether this repo's authors
> considered and rejected option 2, or simply weren't aware of it (Vogen's marker-class attribute is
> a .NET 8+, relatively less-publicized feature), is not stated anywhere in the repo. **This is worth
> a follow-up conversation, not a silent doc correction** — the hand-written converters work fine as
> written, but the CLAUDE.md/code-comment rationale for why they're hand-written is incomplete given
> what Vogen 8.0.7 actually supports.

For **struct**-backed value objects specifically, VG-FAQ states no EF Core configuration is needed at
all: "For struct-based VOs, no configuration needed." Both `ProductId` and `Money` in this repo are
`readonly partial struct` (Repo) — meaning, per this FAQ claim, the hand-written converters in
`ValueConverters.cs` might not even be structurally required for EF Core to persist them correctly
without any converter at all; **this research did not verify what "no configuration needed" means in
practice for a struct VO wrapping a `Guid`/`decimal`** (e.g. whether EF Core's own value-object/owned-type
inference handles it, or whether "no configuration" specifically means "the struct works without a
converter because EF Core already treats a single-primitive-field struct correctly") — flagged as
unverified, see Open Questions.

## 5. System.Text.Json converter generation

`Conversions.SystemTextJson` generates "a System.Text.Json.Serialization.JsonConverter for
serializing the value object to its primitive value" (VG-Integration, quoted) — i.e. the JSON
representation is the bare wrapped primitive (`"3f2504e0-..."` for a `Guid`-backed VO, `12.50` for a
`decimal`-backed one), not a `{ "value": ... }` object wrapper. This repo declares
`Conversions.SystemTextJson` on both `ProductId` and `Money` (Repo) — matching `ProductId.cs`'s own
doc comment: "Serializes as a bare GUID string ... not as an object" (Repo). The default `Conversions`
value (when none is specified) is `TypeConverter | SystemTextJson` (VG-FAQ) — so this repo's explicit
`conversions: Conversions.SystemTextJson` argument is actually redundant with the default for the
JSON part, though it does suppress `Conversions.TypeConverter` generation by being an explicit
override rather than the default flag combination (inference from the flags-enum default stated in
VG-FAQ, not independently re-verified against the attribute's actual default value at the time of
writing — see Open Questions).

## 6. Known limitations / gotchas

- **Records are supported but not automatic** — "If you use features from a later language version,
  for instance, `records` from C# 9, then it will also generate records" (VG-FAQ, quoted) — meaning
  Vogen detects the underlying declared type kind (record struct/record class vs plain struct/class)
  and generates matching code, rather than requiring a separate attribute flag.
- **Struct VOs cannot have user-defined constructors** — Vogen generates them; class-based VOs have
  more flexibility (VG-FAQ). Both of this repo's VOs are structs (Repo), so this restriction applies
  to them.
- **No built-in null handling** — "No built-in support exists" for treating `null` as a valid VO
  state; VG-FAQ points to a separate how-to guide for scenarios that need it, not fetched during this
  research pass.
- **Size overhead** — by default a VO struct carries an extra `_isInitialized` field beyond the
  wrapped primitive, removable via a `VOGEN_NO_VALIDATION` compilation symbol at the cost of losing
  the safety net it provides (VG-FAQ) — this repo does not define `VOGEN_NO_VALIDATION` anywhere in
  its `.csproj`/`Directory.Build.props` files (not independently re-checked file-by-file in this
  research pass, but not encountered while reading `Domain/*.csproj` — see Open Questions if this
  matters to you).
- **Implicit casting bypasses validation/normalization** — found via WebSearch summary of Vogen's
  `casting.html` doc page (not independently re-fetched verbatim in this pass — flagged, see Open
  Questions): if a VO opts into implicit conversion to/from its primitive, that conversion path does
  not run `Validate`/`NormalizeInput`, unlike the generated `From`/`TryFrom` factories. Neither
  `ProductId` nor `Money` in this repo opts into implicit casting (Repo) — both only expose the
  Vogen-generated explicit `Value` property and factories — so this gotcha is not currently reachable
  in this codebase, but would become relevant if a future value object opts in.
- **Generics**: the FAQ content fetched during this research did not surface any specific restriction
  on VOs wrapping a generic type parameter or being declared inside a generic type — this may simply
  mean no notable limitation exists, or that this research didn't surface it. Not independently
  confirmed either way — see Open Questions.

## 7. NHibernate integration

**This repo uses EF Core, not NHibernate.** This section exists as reference material for engineers
who might reach for Vogen value objects on an NHibernate-based project elsewhere — nothing here
implies or requires a change to this repo's own persistence stack.

### 7.1 No first-party Vogen support exists

Unlike EF Core (§4) and System.Text.Json (§5), **Vogen has no NHibernate-specific integration of any
kind** — no generated `IUserType`, no `[NHibernateConverter<T>]`-style attribute, no companion
package. This was checked at every level primary sources allow:

- **VG-Integration's own list of supported integrations** (`integration.html`, fetched and quoted
  directly) enumerates: `IConvertible` (automatic for primitives that implement it),
  `System.Text.Json`, `Newtonsoft.Json`, BSON, a generated **Dapper** `TypeHandler` ("Creates a
  Dapper TypeHandler for converting to and from the type"), a generated **LinqToDB**
  `ValueConverter` ("Creates a LinqToDb ValueConverter for converting to and from the type"), the
  **EF Core** `ValueConverter` covered in §4, ASP.NET Core route-binding support (via the generated
  `TypeConverter`), **protobuf-net**, **ServiceStack.Text** (sets `SerializeFn`/`DeSerializeFn` on
  `JsConfig`), **Microsoft Orleans** (generates and registers a codec and copier), `IXmlSerializable`
  for XML, and `IMessagePackFormatter` for MessagePack. **NHibernate does not appear anywhere in this
  list** (VG-Integration).
- **The Vogen README** (`SteveDunn/Vogen`'s `README.md`, fetched in full and searched) contains zero
  occurrences of "NHibernate" or "Hibernate" (VG-Readme).
- **A direct GitHub Issues Search API query** for `repo:SteveDunn/Vogen NHibernate` returned `0`
  results at query time (GH-IssueSearch) — i.e. no open or closed issue, PR, or discussion thread in
  the Vogen repo currently mentions NHibernate. (A zero-result search is weaker evidence than a
  documented absence — it only shows nobody has filed a matching, indexed issue — but combined with
  the README and integration-list checks above, it corroborates the same conclusion from a third
  angle.)
- **A direct NuGet Search API query** for `Vogen NHibernate` returned `0` hits (NuGet-Search) — no
  `Vogen.NHibernate` or similarly named companion package exists on NuGet, first-party or
  community-maintained.

**Conclusion: this is a hand-roll-it-yourself scenario, not a "found the doc page" scenario.** Every
claim in the rest of this section describes the general NHibernate mechanism an engineer would use,
not anything Vogen ships.

### 7.2 The mechanism you'd hand-write: NHibernate's `IUserType`

NHibernate's standard extension point for mapping a non-primitive, non-entity property (exactly what
a Vogen value object is — a wrapper with a `Value` property and a `From`/`TryFrom` factory, no public
parameterless constructor) to a single database column is the `IUserType` interface, declared in
`src/NHibernate/UserTypes/IUserType.cs` (NH-IUserType, quoted from the interface's own XML doc
comments):

- Purpose: "The interface to be implemented by user-defined types," which "abstracts user code from
  future changes to the `IType` interface, simplifies the implementation of custom types and hides
  certain 'internal interfaces' from user code" (NH-IUserType).
- `SqlTypes` — "The SQL types for the columns mapped by this type" (NH-IUserType) — the NHibernate
  analogue of an EF Core `ValueConverter`'s provider-side type.
- `ReturnedType` — "The type returned by `NullSafeGet()`" (NH-IUserType) — i.e. the CLR type the
  mapped property actually holds (here, the Vogen value object type itself, e.g. `ProductId`).
- `IsMutable` — "Are objects of this type mutable?" (NH-IUserType) — Vogen value objects are
  immutable value types/records (§1), so this would return `false`.
- `NullSafeGet` — "Retrieve an instance of the mapped class from an ADO resultset. Implementors
  should handle possibility of null values" (NH-IUserType) — this is where the hand-written adapter
  would call the Vogen-generated `From`/`TryFrom` factory on the raw primitive read from the
  resultset.
- `NullSafeSet` — "Write an instance of the mapped class to a prepared statement. Implementors should
  handle possibility of null values" (NH-IUserType) — the inverse: read the value object's `Value`
  property and write the primitive to the parameter.
- `Equals` / `GetHashCode` — "Compare two instances of the class mapped by this type for persistent
  'equality'" / "Get a hashcode for the instance, consistent with persistence 'equality'"
  (NH-IUserType) — Vogen already generates structural `IEquatable<T>`/`GetHashCode()` on the value
  object (§1), so these would likely just delegate to the value object's own implementation.
- `DeepCopy`, `Assemble`, `Disassemble` — round out NHibernate's dirty-checking and second-level-cache
  machinery ("Return a deep copy of the persistent state...", "Reconstruct an object from the
  cacheable representation," "Transform the object into its cacheable representation,"
  NH-IUserType).

As of the source read during this research pass, `IUserType` is a **non-generic** interface (its
methods work in terms of `object`) (NH-IUserType) — unlike EF Core's generic
`ValueConverter<TModel, TProvider>` that Vogen's own `[EfCoreConverter<T>]` output wraps (§4). An
NHibernate `IUserType` implementation for a Vogen value object would therefore need its own casts
between `object` and the concrete value-object/primitive types, done by hand inside `NullSafeGet`/
`NullSafeSet`.

NHibernate's official docs site briefly acknowledges custom type mapping as a supported mechanism —
`doc/nhibernate-reference/mapping.html` states a mapped property's `type` attribute can name "the
class name of a custom type" and that "you can write your own mapping types and implement your own
custom conversion strategies" (NH-Mapping, quoted from the fetched excerpt). **This fetch did not
reach that chapter's dedicated `IUserType` walkthrough** (the excerpt returned covered an earlier,
unrelated part of the mapping chapter) — the `IUserType` source itself (NH-IUserType above) is the
stronger, more complete primary source used in this section; see Open Questions for the gap.

### 7.3 Structural parallel to this repo's own EF Core converters

This repo already establishes the precedent this pattern would follow: §4 documents that
`Infrastructure/ValueConverters.cs` hand-writes `ProductIdValueConverter`/`MoneyValueConverter`
instead of using either of Vogen's generated EF Core options, specifically to avoid giving
`MediatrUnionPoc.Domain` a compile-time dependency on EF Core types (Repo, §4 above). An NHibernate
`IUserType` implementation would play **exactly the same architectural role** EF Core's
`ValueConverter` plays here: a hand-written adapter living in the infrastructure/persistence layer
that calls the Vogen-generated `From`/`TryFrom`/`Value` members from the outside, so the `Domain`
project stays free of any ORM reference — NHibernate's own or otherwise. The only difference from
this repo's actual situation is that for NHibernate, hand-writing the adapter isn't a choice made in
preference to a Vogen-generated alternative (as it is for EF Core, per §4's open question) — it's the
**only** option, because Vogen generates nothing NHibernate-shaped at all (§7.1).

## 8. Example: a reusable Vogen `IUserType` + Fluent NHibernate convention

§7 established the mechanism (hand-write an `IUserType`) and its architectural role (structurally
identical to §4's hand-written EF Core `ValueConverter`s). This section works through what that
looks like concretely, as a **generic, reusable adapter** rather than one hand-written `IUserType`
per Vogen value object — plus a Fluent NHibernate convention that applies it automatically to any
property typed as a Vogen value object, so individual class mappings never need a manual
`.CustomType(...)` call. The example is deliberately generic over any `[ValueObject<T>]` type, not
tied to this repo's own `ProductId`/`Money` (this repo uses EF Core, not NHibernate — see §7's own
framing note).

### 8.1 `IUserType`'s current signature, re-verified

§7.2 flagged that its member list was sourced from `IUserType`'s XML doc comments without
independently re-verifying the exact parameter types (`IDataReader` vs `DbDataReader`, etc.). Before
writing the example below, `src/NHibernate/UserTypes/IUserType.cs` was re-fetched directly from
`raw.githubusercontent.com/nhibernate/nhibernate-core/master/...` (NH-IUserType) and quoted here in
full to settle that question:

```csharp
using System.Data.Common;
using NHibernate.Engine;
using NHibernate.SqlTypes;

namespace NHibernate.UserTypes
{
    public interface IUserType
    {
        SqlType[] SqlTypes { get; }
        System.Type ReturnedType { get; }
        bool Equals(object x, object y);
        int GetHashCode(object x);
        object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner);
        void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session);
        object DeepCopy(object value);
        bool IsMutable { get; }
        object Replace(object original, object target, object owner);
        object Assemble(object cached, object owner);
        object Disassemble(object value);
    }
}
```

Two corrections to §7.2, both confirmed by this re-fetch (NH-IUserType):

1. **§7.2's hedge about `NullSafeGet`/`NullSafeSet`'s parameter types was worth raising, and the
   answer is `DbDataReader`/`DbCommand`** — the modern `System.Data.Common` ADO.NET base classes,
   not the older `IDataReader`/`IDbCommand` interfaces. `session` is `ISessionImplementor` as
   §7.2 already inferred from the doc comments.
2. **§7.2's member list omitted `Replace(object original, object target, object owner)`** — used
   during merge to reconcile a detached entity's value with the managed entity's value. It isn't a
   signature-hedge issue (§7.2 never claimed a specific count of members), but it is a gap in that
   section's inventory, corrected here rather than by editing §7.2 itself, per this pass's scope.

Per `IUserType`'s own doc comment (also confirmed in this re-fetch, corroborating NH-Mapping's
narrower excerpt from §7.2): "Implementers must declare a public default constructor." A closed
generic `VogenUserType<TValueObject, TPrimitive>` satisfies this as long as neither type parameter
requires constructor arguments to resolve — true for Vogen VOs, whose only construction path is the
static `From`/`TryFrom` factory (§1), never a constructor.

### 8.2 `VogenUserType<TValueObject, TPrimitive>`

The goal is one `IUserType` implementation that works for *any* Vogen value object, not one
hand-written class per VO. It locates the Vogen-generated `static TValueObject From(TPrimitive)`
factory and the `Value` property once per closed generic instantiation (the JIT already caches one
`VogenUserType<ProductId, Guid>`/`VogenUserType<Money, decimal>` etc. per distinct
`TValueObject`/`TPrimitive` pair), compiles each into a delegate via `System.Linq.Expressions`, and
never uses reflection again on the hot path:

```csharp
using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.Type;
using NHibernate.UserTypes;

/// <summary>
/// A generic NHibernate <see cref="IUserType"/> for any Vogen <c>[ValueObject&lt;TPrimitive&gt;]</c>
/// type. Maps a single NOT NULL database column of <typeparamref name="TPrimitive"/> to/from the
/// Vogen-generated <c>Value</c> property and <c>From(TPrimitive)</c> factory of
/// <typeparamref name="TValueObject"/>.
/// </summary>
/// <remarks>
/// Vogen value objects are typically <c>readonly partial struct</c>s with no null representation
/// (§6 of this document) — this type therefore assumes a NOT NULL column and throws on a NULL read
/// rather than silently returning <c>default</c>. If a column genuinely needs to be nullable, either
/// map it via a class-based Vogen VO and let <see cref="NullSafeGet"/> return a CLR <c>null</c>
/// (swap the throw below for <c>return null;</c>), or wrap the mapped property type in a
/// nullable-aware variant of this adapter that checks <c>DBNull</c> before ever calling
/// <see cref="Value"/>/<c>From</c> — Vogen's own struct VOs cannot represent "no value" themselves
/// (VG-FAQ, §6), so that decision has to live in the adapter, not the VO.
/// </remarks>
/// <typeparam name="TValueObject">The Vogen-generated value object type, e.g. <c>ProductId</c>.</typeparam>
/// <typeparam name="TPrimitive">The primitive it wraps, e.g. <c>Guid</c>.</typeparam>
public sealed class VogenUserType<TValueObject, TPrimitive> : IUserType
    where TPrimitive : notnull
{
    private static readonly Func<TPrimitive, TValueObject> FromFactory = BuildFromFactory();
    private static readonly Func<TValueObject, TPrimitive> ValueGetter = BuildValueGetter();
    private static readonly SqlType[] CachedSqlTypes = { ResolveSqlType() };

    /// <inheritdoc />
    public SqlType[] SqlTypes => CachedSqlTypes;

    /// <inheritdoc />
    public Type ReturnedType => typeof(TValueObject);

    /// <inheritdoc />
    public bool IsMutable => false; // Vogen VOs are immutable (§1).

    /// <inheritdoc />
    bool IUserType.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.Equals(y); // Vogen generates structural IEquatable<T> (§1) — delegate to it.
    }

    /// <inheritdoc />
    public int GetHashCode(object x) => x?.GetHashCode() ?? 0;

    /// <inheritdoc />
    public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
    {
        int ordinal = rs.GetOrdinal(names[0]);
        if (rs.IsDBNull(ordinal))
        {
            throw new InvalidOperationException(
                $"Column '{names[0]}' was NULL, but {typeof(TValueObject)} is a non-nullable Vogen " +
                "value object with no null representation. Map the column NOT NULL, or adapt this " +
                "type to return null for a class-based Vogen VO (see the remarks on this type).");
        }

        TPrimitive primitive = (TPrimitive)Convert.ChangeType(rs.GetValue(ordinal), typeof(TPrimitive));
        return FromFactory(primitive)!;
    }

    /// <inheritdoc />
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
    {
        DbParameter parameter = cmd.Parameters[index];
        parameter.Value = value is TValueObject valueObject
            ? (object)ValueGetter(valueObject)
            : DBNull.Value;
    }

    /// <inheritdoc />
    public object DeepCopy(object value) => value; // Immutable — safe to hand back the same instance.

    /// <inheritdoc />
    public object Replace(object original, object target, object owner) => original; // Immutable.

    /// <inheritdoc />
    public object Assemble(object cached, object owner) =>
        cached is TPrimitive primitive ? FromFactory(primitive)! : cached;

    /// <inheritdoc />
    public object Disassemble(object value) =>
        value is TValueObject valueObject ? (object)ValueGetter(valueObject) : value;

    private static Func<TPrimitive, TValueObject> BuildFromFactory()
    {
        MethodInfo? fromMethod = typeof(TValueObject).GetMethod(
            "From",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(TPrimitive) },
            modifiers: null);

        if (fromMethod is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TValueObject)} has no public static From({typeof(TPrimitive).Name}) " +
                "factory — is it really a Vogen [ValueObject<T>] type, and does TPrimitive match " +
                "the type it wraps?");
        }

        ParameterExpression primitiveParam = Expression.Parameter(typeof(TPrimitive), "primitive");
        MethodCallExpression call = Expression.Call(fromMethod, primitiveParam);
        return Expression.Lambda<Func<TPrimitive, TValueObject>>(call, primitiveParam).Compile();
    }

    private static Func<TValueObject, TPrimitive> BuildValueGetter()
    {
        PropertyInfo? valueProperty = typeof(TValueObject).GetProperty(
            "Value", BindingFlags.Public | BindingFlags.Instance);

        if (valueProperty is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TValueObject)} has no public Value property — is it really a Vogen " +
                "[ValueObject<T>] type?");
        }

        ParameterExpression voParam = Expression.Parameter(typeof(TValueObject), "valueObject");
        MemberExpression access = Expression.Property(voParam, valueProperty);
        return Expression.Lambda<Func<TValueObject, TPrimitive>>(access, voParam).Compile();
    }

    private static SqlType ResolveSqlType()
    {
        // NHibernateUtil.GuessType maps common CLR primitives (Guid, string, decimal, int, ...) to
        // NHibernate's built-in NullableType-derived IType instances, which expose a single SqlType.
        // This covers every primitive Vogen commonly wraps (§1) but is not exhaustive — an
        // unrecognized TPrimitive throws here at type-initialization time rather than silently
        // mis-mapping a column.
        if (NHibernateUtil.GuessType(typeof(TPrimitive)) is NullableType nullableType)
        {
            return nullableType.SqlType;
        }

        throw new NotSupportedException(
            $"Could not resolve a SqlType for primitive '{typeof(TPrimitive)}' wrapped by " +
            $"{typeof(TValueObject)}. Supply an explicit SqlType for this primitive.");
    }
}
```

### 8.3 A Fluent NHibernate convention that applies it automatically

Fluent NHibernate's convention API (`FluentNHibernate.Conventions`, fetched directly from
`raw.githubusercontent.com/FluentNHibernate/fluent-nhibernate/master/...` — FNH-Conventions) defines
property-level conventions as:

```csharp
// FluentNHibernate.Conventions.IConvention<TInspector, TInstance>
public interface IConvention<TInspector, TInstance> : IConvention
    where TInspector : IInspector
    where TInstance : TInspector
{
    void Apply(TInstance instance);
}

// FluentNHibernate.Conventions.IConventionAcceptance<TInspector>
public interface IConventionAcceptance<TInspector>
    where TInspector : IInspector
{
    void Accept(IAcceptanceCriteria<TInspector> criteria);
}

// FluentNHibernate.Conventions.IPropertyConvention / IPropertyConventionAcceptance
public interface IPropertyConvention : IConvention<IPropertyInspector, IPropertyInstance> { }
public interface IPropertyConventionAcceptance : IConventionAcceptance<IPropertyInspector> { }
```

`IPropertyInstance` (same source) exposes the `CustomType` overload this convention needs —
confirmed directly from `Conventions/Instances/IPropertyInstance.cs` (FNH-Conventions):

```csharp
void CustomType<T>();
void CustomType(System.Type type);
// ...additional CustomType/CustomType(string) overloads omitted for brevity
```

and `IPropertyInspector.Type` returns a `TypeReference` whose `GetUnderlyingSystemType()` resolves
back to the mapped property's CLR `System.Type` (confirmed from `MappingModel/TypeReference.cs`,
FNH-Conventions).

**Detecting "is this property a Vogen value object" at runtime** ideally checks for Vogen's own
attribute rather than duck-typing. Vogen ships `Vogen.ValueObjectAttribute<T>` — confirmed by
fetching `src/Vogen.SharedTypes/ValueObjectAttribute.cs` directly from
`raw.githubusercontent.com/SteveDunn/Vogen/main/...` during this research pass (VG-Readme's own repo,
same primary source as the rest of this document) — declared as:

```csharp
namespace Vogen
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class ValueObjectAttribute<T> : ValueObjectAttribute { /* ... */ }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class ValueObjectAttribute : Attribute { /* ... */ }
}
```

The useful detail: `ValueObjectAttribute<T>` derives from the **non-generic** `ValueObjectAttribute`.
That means detection doesn't need to reflect over open generic type definitions at all — checking
for the non-generic base via `Type.GetCustomAttributes(typeof(Vogen.ValueObjectAttribute), false)` on
a property's type finds any `[ValueObject<T>]`-decorated type regardless of what `T` is. This part is
fetched-and-confirmed, not a guess.

```csharp
using System;
using System.Reflection;
using FluentNHibernate.Conventions;
using FluentNHibernate.Conventions.AcceptanceCriteria;
using FluentNHibernate.Conventions.Inspections;
using FluentNHibernate.Conventions.Instances;

/// <summary>
/// Applies <see cref="VogenUserType{TValueObject,TPrimitive}"/> to any property whose type is a
/// Vogen <c>[ValueObject&lt;T&gt;]</c>, so individual class mappings never need a hand-written
/// <c>.CustomType(...)</c> call for a Vogen-typed property.
/// </summary>
public sealed class VogenValueObjectConvention : IPropertyConvention, IPropertyConventionAcceptance
{
    /// <inheritdoc />
    public void Accept(IAcceptanceCriteria<IPropertyInspector> criteria) =>
        criteria.Expect(inspector => IsVogenValueObject(inspector.Type.GetUnderlyingSystemType()));

    /// <inheritdoc />
    public void Apply(IPropertyInstance instance)
    {
        Type valueObjectType = instance.Type.GetUnderlyingSystemType();
        Type primitiveType = GetWrappedPrimitiveType(valueObjectType);
        Type userType = typeof(VogenUserType<,>).MakeGenericType(valueObjectType, primitiveType);

        instance.CustomType(userType);
    }

    /// <summary>
    /// Primary detection: Vogen's own <c>[ValueObject&lt;T&gt;]</c> attribute (confirmed against
    /// Vogen's source, see §8.3). Requires the persistence-layer assembly to reference
    /// <c>Vogen.SharedTypes</c> (the attribute's assembly) — the same trade-off §4/§7.3 already
    /// accept for a hand-written adapter that has to know about the value object's shape anyway.
    /// </summary>
    private static bool IsVogenValueObject(Type candidate) =>
        candidate.GetCustomAttributes(typeof(Vogen.ValueObjectAttribute), inherit: false).Length > 0;

    /// <summary>
    /// Fallback duck-typing detection, for a setup that can't take a compile-time dependency on the
    /// <c>Vogen</c> package from the mapping-convention project. <strong>Lower confidence than the
    /// attribute check above</strong> — it was not cross-checked against every other library that
    /// happens to expose a public static <c>From(T)</c> plus a public <c>Value</c> property, so it
    /// could false-positive on an unrelated type shaped the same way. Prefer the attribute check
    /// unless that dependency is genuinely unavailable.
    /// </summary>
    private static bool LooksLikeVogenValueObject(Type candidate)
    {
        PropertyInfo? valueProperty = candidate.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProperty is null)
        {
            return false;
        }

        return candidate.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(m => m.Name == "From" && m.GetParameters().Length == 1);
    }

    private static Type GetWrappedPrimitiveType(Type valueObjectType)
    {
        PropertyInfo? valueProperty = valueObjectType.GetProperty(
            "Value", BindingFlags.Public | BindingFlags.Instance);

        if (valueProperty is null)
        {
            throw new InvalidOperationException($"{valueObjectType} has no public Value property.");
        }

        return valueProperty.PropertyType;
    }
}
```

`LooksLikeVogenValueObject` above needs `using System.Linq;` for `Any` if copied as-is — omitted from
the using list intentionally to keep it visible that this is illustrative code, not a compiled unit
(see §8.4).

The convention is registered the same way any Fluent NHibernate convention is, via
`Conventions.Add<VogenValueObjectConvention>()` on the `FluentMappings` configuration — not shown
here since that registration API wasn't independently re-verified during this pass and isn't
specific to this pattern.

### 8.4 Status of this example

**Everything in §8.2/§8.3 is illustrative example code written for this research document. It has
not been compiled or tested against a real NHibernate + Fluent NHibernate + Vogen project.** This
mirrors how §7.2/§7.3 already flag themselves as descriptive/inferential rather than a built and
verified artifact (§7.2's closing note; Open Questions item 8 below). What *was* independently
verified against primary sources for this section specifically: `IUserType`'s exact current member
list and parameter types (§8.1, NH-IUserType, re-fetched verbatim), Fluent NHibernate's convention
interfaces and the `CustomType`/`TypeReference.GetUnderlyingSystemType()` APIs the convention relies
on (§8.3, FNH-Conventions, re-fetched verbatim), and the exact runtime-visible `Vogen.ValueObjectAttribute<T>` /
`Vogen.ValueObjectAttribute` type names and inheritance relationship (§8.3, fetched from Vogen's own
source). What was **not** verified: whether `NHibernateUtil.GuessType` and `NullableType.SqlType`
resolve every primitive type Vogen can wrap the way §8.2's `ResolveSqlType` assumes, whether
`Conventions.Add<T>()` is still the correct registration call in a current Fluent NHibernate release,
and whether the whole example actually compiles against current NuGet package versions of
`NHibernate`/`FluentNHibernate`/`Vogen` side by side — none of that was checked by running a build.

---

## Open questions / where documentation is thin

1. **Several Vogen claims in §1 and §4 (`TryFrom`'s exact signature/return type, the
   `[EfCoreConverter<T>]` marker-class snippet, and the implicit-casting-bypasses-validation claim)
   were sourced via WebSearch summaries of official doc pages, not independently re-fetched and
   quoted verbatim by this research pass** — each is flagged individually inline where used. If any
   of these become load-bearing for a design decision, re-fetch
   `stevedunn.github.io/Vogen/working-with-ids.html`, `.../efcoreintegrationhowto.html`, and
   `.../casting.html` directly and quote them verbatim rather than relying on this document's
   secondhand summary.
2. **Whether Vogen struct VOs truly need zero EF Core configuration** (VG-FAQ's "For struct-based
   VOs, no configuration needed") was not reconciled against the fact that this repo *does*
   hand-write converters for its two struct VOs. Either the FAQ's claim has caveats not surfaced in
   the fetched excerpt (e.g. it may mean "no *extra* configuration beyond a normal owned-type/complex-type
   mapping," not "EF Core round-trips it with zero configuration of any kind"), or this repo's
   converters are doing something EF Core's own struct-VO inference wouldn't otherwise do correctly
   (e.g. surfacing the wrapped `Guid`/`decimal` as the actual column type rather than a multi-column
   complex type). Worth a direct, hands-on check against this repo's own EF Core model rather than
   trusting either source blindly.
3. **Why this repo hand-writes EF Core converters instead of using Vogen's `[EfCoreConverter<T>]`
   marker-class option** (§4's flagged note) is a genuine gap between the repo's stated rationale and
   what Vogen 8.0.7 actually offers — not a doc-thinness issue on Vogen's side, but worth raising
   with whoever owns this repo's `CLAUDE.md`/code comments.
4. **Vogen generics support** — no explicit statement of support or limitation was located for a VO
   declared over a generic type parameter or nested inside a generic type. Absence of a stated
   limitation is not confirmation that none exists.
5. This document reflects the state of Vogen's docs/repo content as fetched on 2026-09-18, against
   the exact version pinned in this repo (Vogen 8.0.7). Re-verify against the pinned version in
   `Directory.Packages.props` before trusting a claim here against a newer install.
6. **§7's "no NHibernate support" conclusion rests on absence-of-evidence checks** (a README text
   search, an integration-page enumeration that doesn't name NHibernate, and zero-result GitHub/NuGet
   searches), not on a Vogen maintainer statement explicitly ruling it out. None of Vogen's GitHub
   Discussions were browsed manually (only searched via the Issues Search API, which may not index
   Discussions the same way as Issues/PRs) — if NHibernate support is ever load-bearing for a
   decision, browse `github.com/SteveDunn/Vogen/discussions` directly rather than trusting the API
   search's coverage.
7. **NH-Mapping's citation in §7.2 is thin** — the fetch of `nhibernate-reference/mapping.html` only
   returned an excerpt covering an earlier part of the chapter (the `type` attribute's general
   description), not the chapter's dedicated custom-type/`IUserType` walkthrough that presumably
   exists further down the same page or in a linked sub-page. §7.2's technical detail on `IUserType`
   itself is sourced from the interface's own XML doc comments (NH-IUserType) instead, which is
   arguably the stronger primary source, but the official narrative docs' fuller explanation (with
   any usage caveats NHibernate itself calls out) was not independently confirmed in this pass — worth
   a direct re-fetch of that page (or `nhibernate.info`'s search) before treating §7.2 as a complete
   how-to.
8. **No working code example was built or compiled** for §7.2's proposed `IUserType`
   implementation — unlike §3/§4/§5, which describe patterns already present and building in this
   repo's own source, §7 is entirely descriptive/inferential (an engineer following it would still
   need to write and test the actual `IUserType` class against a real NHibernate mapping).
9. **§8's `VogenUserType<TValueObject, TPrimitive>` and `VogenValueObjectConvention` were written for
   this document and never compiled or run against a real NHibernate + Fluent NHibernate + Vogen
   project** (§8.4). Before copy-pasting either into production code: verify `ResolveSqlType`'s use
   of `NHibernateUtil.GuessType`/`NullableType.SqlType` actually resolves every primitive Vogen can
   wrap (only `IUserType`'s own signatures and Fluent NHibernate's convention/`CustomType` APIs were
   independently re-fetched and confirmed — the NHibernate `Type` namespace's SQL-type-guessing
   behavior was not); confirm `Conventions.Add<T>()` (or whatever the current registration call is)
   against a current Fluent NHibernate release; and build the whole example against the actual NuGet
   package versions intended for use, side by side, before trusting it.

# Vogen: avoiding primitive obsession

Part of the [documentation](index.md).

> [!TIP]
> Deeper, citation-backed research behind this section lives in
> [`docs/research/vogen-research.md`](research/vogen-research.md) — primary sources (Vogen's
> README and official docs site) for the factory/equality/conversion behavior described below, and
> the defense-in-depth reasoning behind the EF Core converter choice.

[Vogen](https://github.com/SteveDunn/Vogen) is a [source generator](glossary.md#c-language-concepts) that
turns a bare primitive (`Guid`, `decimal`, `string`, ...) into a distinct, validated
[value type](glossary.md#vogen-vocabulary) — so `ProductId` and an unrelated `Guid` parameter can never be
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
  equal; [`record`](glossary.md#c-language-concepts)-style value semantics without needing `record` (this has
  to be a `struct` per Vogen's own constraints, and `readonly partial struct` is the declaration
  shape it generates into).
- **Conversions**, opt-in per flag on the attribute. This repo uses `Conversions.SystemTextJson`
  only, so `ProductId` serializes as a bare GUID string (`"..."`), not as a wrapper object — see
  [`ProductDto`](../src/MediatrUnionPoc.Application/Features/Products/Common/ProductDto.cs) for where
  that matters on the wire.

`Money` ([`Money.cs`](../src/MediatrUnionPoc.Domain/Money.cs)) follows the identical pattern over
`decimal`, rejecting negative amounts.

Two Vogen-specific pitfalls we hit while building this — full detail in
[Notes and gotchas](notes-and-gotchas.md#notes-and-gotchas) — are worth knowing about up front if you add your own
value object: don't set `PrivateAssets="all"` on the `Vogen` package reference, and don't reach
for Vogen's generated `EfCoreValueConverter` from a persistence-agnostic Domain project (this repo
hand-writes its EF Core converters in Infrastructure instead — see
[`ValueConverters.cs`](../src/MediatrUnionPoc.Infrastructure/ValueConverters.cs)).

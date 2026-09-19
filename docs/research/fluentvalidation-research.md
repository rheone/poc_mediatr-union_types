# FluentValidation — research notes

Compiled 2026-09-18 as source material for engineers working on this repo's pipeline
(`LoggingBehavior` → `ValidationBehavior` → `TransactionBehavior`, wired in
`Application/DependencyInjection.cs`). Every claim is cited inline to its primary source. Secondary
sources (blog posts, aggregator search summaries) are explicitly flagged as such — don't treat them
as authoritative over the primary docs/repo/source below. This document follows the same citation
discipline as `docs/research/csharp-union-type-research.md` — read that one first if you haven't;
the conventions (inline citation tags, flagged secondary sources, an "Open questions" close) are
identical here.

This repo pins **FluentValidation 12.1.1** (+ `FluentValidation.DependencyInjectionExtensions`
12.1.1) in `Directory.Packages.props`. For how FluentValidation's rules run inside MediatR's pipeline
behaviors, see [`mediatr-research.md`](mediatr-research.md) §3 for the worked sequence diagram, and
§2 below for the seam itself.

**Primary sources used:**

- [FluentValidation/FluentValidation GitHub repo](https://github.com/FluentValidation/FluentValidation) — README ("FV-Readme" below)
- [docs.fluentvalidation.net](https://docs.fluentvalidation.net/en/latest/) — official docs site, specifically the `async.html`, `cascade.html`, and `di.html` pages ("FV-Async", "FV-Cascade", "FV-DI" below)
- This repo's own source: `Application/Common/Behaviors/ValidationBehavior.cs`,
  `Application/DependencyInjection.cs`, `Application/Features/Products/Create/*.cs` ("Repo" below)

**Secondary sources:** none used in this document — every claim below is grounded directly in
FluentValidation's own README/docs site or this repo's source.

---

## 1. Core API

### `AbstractValidator<T>` and rule builders

A validator subclasses `AbstractValidator<T>` and declares rules in its constructor via
`RuleFor(x => x.Property)` chains (FV-Readme). This repo's `CreateProductValidator` is a minimal,
canonical example (Repo):

```csharp
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
```

### `ValidationResult` / `ValidationFailure`

Calling `validator.Validate(instance)` (or `ValidateAsync`) returns a `ValidationResult` exposing
`IsValid` (bool) and `Errors` (a list of `ValidationFailure`, each carrying at least a
`PropertyName` and `ErrorMessage`) (FV-Readme). This repo's `ValidationBehavior<TRequest,TResponse>`
projects exactly those two fields per failure into its own `ValidationError(PropertyName,
ErrorMessage)` case type (Repo, `Common/Behaviors/ValidationBehavior.cs`):

```csharp
var failures = results
    .SelectMany(r => r.Errors)
    .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage))
    .ToList();
```

### Cascade modes

FluentValidation has two cascade behaviors, `Continue` (run every validator/rule regardless of
earlier failures) and `Stop` (stop at the first failure) (FV-Cascade). As of FluentValidation 11,
this is controlled at two independent levels — `RuleLevelCascadeMode` (within one `RuleFor` chain)
and `ClassLevelCascadeMode` (across separate `RuleFor` calls in the same validator) — both defaulting
to `Continue` globally via `ValidatorOptions.Global.DefaultRuleLevelCascadeMode` /
`DefaultClassLevelCascadeMode` (FV-Cascade, quoted: "Both of these default to `Continue`"). A prior
single unified `CascadeMode` property "was deprecated in FluentValidation 11 and removed in
FluentValidation 12" (FV-Cascade) — **this repo is pinned to FluentValidation 12.1.1** (Repo,
`Directory.Packages.props`), so the old unified property is not available at all; any FluentValidation
sample code found online written against pre-11 FluentValidation using a single `CascadeMode` setter
will not compile against this repo's pinned version.

### Async validation

`MustAsync` lets a rule run an async predicate (e.g. an external existence check); the containing
validator must then be invoked with `ValidateAsync`, never the synchronous `Validate` — "If your
validator contains asynchronous validators or asynchronous conditions, it's important that you
*always* call *ValidateAsync* on your validator and never *Validate*. If you call *Validate*, then an
exception will be thrown" (FV-Async, quoted verbatim). `ValidateAsync` runs both sync and async rules
in a single pass (FV-Async). This repo's `ValidationBehavior` already always calls `ValidateAsync`
(Repo) — future validators in this repo are therefore free to add `MustAsync` rules without changing
the pipeline behavior itself, since it never calls the sync `Validate` API.

> [!WARNING]
> FluentValidation's own docs additionally warn: "You should not use asynchronous rules when using
> automatic validation with ASP.NET" because ASP.NET's own auto-validation "will always be run
> synchronously (10.x and older) or throw an exception (11.x and newer)" (FV-Async, quoted verbatim).
> This repo does **not** use ASP.NET's built-in MVC/minimal-API auto-validation — it only calls
> `ValidateAsync` manually from `ValidationBehavior` (Repo) — so this particular warning doesn't
> apply here, but it's a real trap for anyone tempted to *also* wire up `[FromBody]`-model
> auto-validation attributes alongside the MediatR pipeline behavior.

### DI registration (`FluentValidation.DependencyInjectionExtensions`)

`AddValidatorsFromAssembly(assembly)` / `AddValidatorsFromAssemblyContaining<TMarker>()` scan an
assembly and register "all public non-abstract validators" (FV-DI, quoted). Default lifetime is
**Scoped**, overridable to `Singleton` or `Transient` (FV-DI, quoted: "By default, these will be
registered as `Scoped`, but you can optionally use `Singleton` or `Transient` instead") — with the
docs' own guidance that "Registering validators as Transient is the simplest and safest option"
(FV-DI, quoted). This repo registers with the bare `AddValidatorsFromAssembly(AssemblyReference)`
call (Repo, `Application/DependencyInjection.cs`), taking FluentValidation's default (Scoped)
lifetime rather than explicitly overriding it — worth knowing if a validator is ever made stateful,
since Scoped validators are shared within one MediatR request's DI scope but not across requests.

---

## 2. The MediatR integration seam — community convention, not official

**FluentValidation itself ships no MediatR integration** — the official DI docs page fetched for
this research makes no mention of MediatR at all (FV-DI: "The documentation provided does not
mention MediatR integration at all. It only references ASP.NET auto-validation..."). Running
FluentValidation validators from inside an `IPipelineBehavior<TRequest,TResponse>` — exactly what
this repo's `ValidationBehavior` does — is **community/practitioner convention, not an officially
documented or Jimmy-Bogard/Jeremy-Skinner-endorsed pattern**; it is extremely widely used (it's the
top hit for "MediatR FluentValidation" in virtually every .NET blog and course), but no primary
source fetched during this research states it as the recommended approach. Flag this explicitly when
writing anything that implies "the sanctioned way" — it's a de facto standard, not a documented one.

See [`mediatr-research.md`](mediatr-research.md) §3 for the sequence diagram showing exactly where
`ValidationBehavior` sits in this repo's pipeline, and how a validation failure short-circuits the
rest of the chain — that short-circuit behavior is this repo's own design
(`IValidatable<TSelf>`/`FromValidationErrors`), not anything FluentValidation or MediatR prescribes.

---

## Open questions / where documentation is thin

1. No primary source describes FluentValidation-in-a-MediatR-behavior as a recommended pattern (§2)
   — treat it as convention, not documented guidance, in anything written for a wider audience.
2. This document reflects the state of FluentValidation's docs/repo content as fetched on
   2026-09-18, against the exact version pinned in this repo (FluentValidation 12.1.1). Re-verify
   against the pinned version in `Directory.Packages.props` before trusting a claim here against a
   newer install — cascade-mode behavior in particular has changed across major versions (§1).
